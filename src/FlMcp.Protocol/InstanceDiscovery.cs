using System.IO.Pipes;
using System.Text.Json;

namespace FlMcp.Protocol;

/// <summary>Private same-user connection metadata for one plugin instance. Never log its token.</summary>
public sealed record FlInstanceEndpoint(int ProcessId, string InstanceId, string Token, string ExecutablePath);

/// <summary>Discovers a specifically selected FL process over a Windows-authenticated local pipe.</summary>
public static class InstanceDiscovery
{
    /// <summary>Maximum framed discovery payload; discovery never carries project data.</summary>
    public const int MaximumMessageBytes = 16 * 1024;

    /// <summary>Returns the current Windows user's discovery pipe name for one process.</summary>
    public static string Name(int processId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        return PipeProtocol.Name(processId) + "-discovery";
    }

    /// <summary>Checks the OS server PID before reading any token-bearing metadata. The whole exchange is bounded.</summary>
    public static async Task<FlInstanceEndpoint> ReadAsync(int processId, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        await using var pipe = new NamedPipeClientStream(".", Name(processId), PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            if (PipePeerIdentity.GetServerProcessId(pipe) != processId)
                throw new IOException("Discovery pipe belongs to a different process than the selected FL instance.");
            var endpoint = await PipeProtocol.ReadAsync<FlInstanceEndpoint>(pipe, deadline.Token, MaximumMessageBytes).ConfigureAwait(false);
            Validate(endpoint);
            if (endpoint.ProcessId != processId) throw new InvalidDataException("Discovery metadata has a mismatched process identity.");
            return endpoint;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The selected FL instance did not respond to discovery within five seconds.");
        }
    }

    internal static void Validate(FlInstanceEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.ProcessId <= 0) throw new InvalidDataException("Discovery requires a positive process identity.");
        if (!Guid.TryParse(endpoint.InstanceId, out var identity) || identity == Guid.Empty)
            throw new InvalidDataException("Discovery requires a nonempty instance UUID.");
        if (string.IsNullOrWhiteSpace(endpoint.Token) || endpoint.Token.Length is < 32 or > 1024)
            throw new InvalidDataException("Discovery requires a valid private connection token.");
        if (string.IsNullOrWhiteSpace(endpoint.ExecutablePath) || !Path.IsPathFullyQualified(endpoint.ExecutablePath) ||
            endpoint.ExecutablePath.Contains('\0')) throw new InvalidDataException("Discovery requires an absolute executable path.");
        if (JsonSerializer.SerializeToUtf8Bytes(endpoint, Messages.Json).Length > MaximumMessageBytes)
            throw new InvalidDataException("Discovery metadata exceeds its 16 KiB limit.");
    }
}

/// <summary>Publishes this process's endpoint to the current Windows user without invoking FL.</summary>
public sealed class InstanceDiscoveryServer
{
    private readonly FlInstanceEndpoint _endpoint;
    private int _running;

    /// <summary>Creates a publisher for this process only, refusing misleading PID metadata.</summary>
    public InstanceDiscoveryServer(FlInstanceEndpoint endpoint)
    {
        InstanceDiscovery.Validate(endpoint);
        if (endpoint.ProcessId != Environment.ProcessId)
            throw new ArgumentException("A discovery server can only publish its own process.", nameof(endpoint));
        _endpoint = endpoint;
    }

    /// <summary>Serves bounded framed responses until cancelled; all listener handles close before returning.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Discovery server is already running.");
        NamedPipeServerStream? listener = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            listener = CreateListener(first: true);
            while (!ct.IsCancellationRequested)
            {
                await listener.WaitForConnectionAsync(ct).ConfigureAwait(false);
                var connected = listener;
                try
                {
                    // Keep a pending listener before closing the connected one. This also
                    // keeps first-instance ownership continuously held between requests.
                    listener = CreateListener(first: false);
                    await SendAsync(connected, ct).ConfigureAwait(false);
                }
                finally { await connected.DisposeAsync().ConfigureAwait(false); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            if (listener is not null) await listener.DisposeAsync().ConfigureAwait(false);
            Volatile.Write(ref _running, 0);
        }
    }

    private NamedPipeServerStream CreateListener(bool first) => new(InstanceDiscovery.Name(_endpoint.ProcessId),
        PipeDirection.InOut, 2, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly |
        (first ? PipeOptions.FirstPipeInstance : PipeOptions.None), 0, InstanceDiscovery.MaximumMessageBytes);

    private async Task SendAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await PipeProtocol.WriteAsync(pipe, _endpoint, deadline.Token, InstanceDiscovery.MaximumMessageBytes).ConfigureAwait(false);
            // Keep this server handle alive until the client closes, so its OS PID
            // remains queryable even when the response fits entirely in the pipe buffer.
            await pipe.ReadAsync(new byte[1], deadline.Token).ConfigureAwait(false);
        }
        catch (IOException) { /* A client can disconnect without taking discovery down. */ }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* Bound a client that never reads. */ }
    }
}
