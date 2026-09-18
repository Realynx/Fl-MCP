using System.IO.Pipes;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

public interface IBridgeClient
{
    Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct);
    Task<JsonElement> CallAttachedAsync(int processId, string token, string leaseToken, string operation, object arguments, int timeoutSeconds, CancellationToken ct) =>
        throw new NotSupportedException("This bridge client does not support attachments.");
}

/// <summary>The request may still be running in FL; the owner must not terminate that process.</summary>
public sealed class BridgeCompletionUnknownException(Exception inner) : IOException(
    "The embedded Python bridge disconnected before completion was acknowledged. FL remains open until its interpreter is known to be idle.", inner);

public sealed class BridgeClient : IBridgeClient
{
    public Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct) =>
        CallCoreAsync(processId, token, null, operation, arguments, timeoutSeconds, ct);

    public Task<JsonElement> CallAttachedAsync(int processId, string token, string leaseToken, string operation, object arguments, int timeoutSeconds, CancellationToken ct) =>
        CallCoreAsync(processId, token, leaseToken, operation, arguments, timeoutSeconds, ct);

    private static async Task<JsonElement> CallCoreAsync(int processId, string token, string? leaseToken, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var budget = TimeSpan.FromSeconds(timeoutSeconds + 2);
        deadline.CancelAfter(budget);
        try
        {
            return await ExchangeAsync(processId, token, leaseToken, operation, arguments, timeoutSeconds, deadline.Token).ConfigureAwait(false);
        }
        // The server's OWN deadline, not the caller's: a bare OperationCanceledException here says nothing
        // about what timed out, so name the operation and the budget instead (live: fl_plugins_list surfaced
        // as the MCP SDK's detail-free "An error occurred invoking ..." because nothing converted this).
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"The FL bridge did not answer '{operation}' within {budget.TotalSeconds:0.#} s. " +
                "FL may be busy, showing a modal dialog, or the operation is genuinely slower than this tool's budget; " +
                "check FL's window, confirm the project state and retry. The request was not retried automatically.");
        }
    }

    private static async Task<JsonElement> ExchangeAsync(int processId, string token, string? leaseToken, string operation,
        object arguments, int timeoutSeconds, CancellationToken deadline)
    {
        await using var pipe = new NamedPipeClientStream(".", PipeProtocol.Name(processId), PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(deadline).ConfigureAwait(false);
        if (leaseToken is not null || operation == "attach")
        {
            if (PipePeerIdentity.GetServerProcessId(pipe) != processId)
                throw new IOException("The bridge belongs to another process; attachment was refused.");
        }
        var response = await SendAndReadAsync(pipe, token, leaseToken, operation, arguments, timeoutSeconds, deadline).ConfigureAwait(false);
        if (response.Error is not null) throw new InvalidOperationException(response.Error);
        return response.Result ?? throw new InvalidDataException("Bridge returned no result.");
    }

    private static async Task<BridgeResponse> SendAndReadAsync(Stream pipe, string token, string? leaseToken, string operation,
        object arguments, int seconds, CancellationToken ct)
    {
        var acknowledged = false;
        try
        {
            await PipeProtocol.WriteAsync(pipe, new BridgeRequest(token, operation, Messages.Element(arguments), seconds) { LeaseToken = leaseToken }, ct).ConfigureAwait(false);
            var response = operation is "python_call" or "python_execute"
                ? await ReadDrainedAsync(pipe, ct).ConfigureAwait(false)
                : await PipeProtocol.ReadAsync<BridgeResponse>(pipe, ct).ConfigureAwait(false);
            acknowledged = true;
            ct.ThrowIfCancellationRequested();
            return response;
        }
        catch (Exception ex) when (operation == "python_execute" && !acknowledged)
        {
            throw new BridgeCompletionUnknownException(ex);
        }
    }

    private static async Task<BridgeResponse> ReadDrainedAsync(Stream pipe, CancellationToken ct)
    {
        // Cancellation is signalled without closing the pipe. The plugin acknowledges only after
        // its shared dispatcher drains the native call; render/close must not race that call.
        var read = PipeProtocol.ReadAsync<BridgeResponse>(pipe, CancellationToken.None);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => cancelled.TrySetResult());
        if (await Task.WhenAny(read, cancelled.Task).ConfigureAwait(false) != read)
        {
            try { await pipe.WriteAsync(new byte[] { 0 }, CancellationToken.None).ConfigureAwait(false); }
            catch (IOException) { }
        }
        var response = await read.ConfigureAwait(false);
        return response;
    }
}
