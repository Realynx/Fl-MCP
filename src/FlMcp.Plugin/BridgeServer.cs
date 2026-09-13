using System.IO.Pipes;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Plugin;

public sealed class BridgeServer(
    string pipeName,
    string token,
    Func<string, JsonElement, CancellationToken, Task<JsonElement>> dispatch,
    Action<string> log,
    Func<int, BridgeRequest, CancellationToken, Task<JsonElement>>? contextualDispatch = null)
{
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await ServeConnectionAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                log($"[fl-mcp] Bridge connection failed: {ex.Message}");
                try { await Task.Delay(100, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            }
        }
    }

    private async Task ServeConnectionAsync(CancellationToken ct)
    {
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var request = await PipeProtocol.ReadAsync<BridgeRequest>(pipe, deadline.Token).ConfigureAwait(false);
        if (!PipeProtocol.TokenMatches(token, request.Token))
        {
            await PipeProtocol.WriteAsync(pipe, new BridgeResponse(null, "Session authentication failed."), deadline.Token).ConfigureAwait(false);
            return;
        }
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 1, request.Operation == "python_execute" ? 300 : 60)));
        using var monitorLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var monitor = MonitorDisconnectAsync(pipe, deadline, monitorLifetime.Token);
        var peer = contextualDispatch is null ? 0 : PipePeerIdentity.GetClientProcessId(pipe);
        var response = await ExecuteAsync(request, peer, deadline.Token).ConfigureAwait(false);
        await monitorLifetime.CancelAsync().ConfigureAwait(false);
        await monitor.ConfigureAwait(false);
        using var replyDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        replyDeadline.CancelAfter(TimeSpan.FromSeconds(5));
        await PipeProtocol.WriteAsync(pipe, response, replyDeadline.Token).ConfigureAwait(false);
    }

    private static async Task MonitorDisconnectAsync(Stream pipe, CancellationTokenSource operation, CancellationToken ct)
    {
        try
        {
            // One request per connection: EOF or any unexpected further input ends this operation.
            await pipe.ReadAsync(new byte[1], ct).ConfigureAwait(false);
            await operation.CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (IOException) { await operation.CancelAsync().ConfigureAwait(false); }
    }

    private async Task<BridgeResponse> ExecuteAsync(BridgeRequest request, int peer, CancellationToken ct)
    {
        try
        {
            return new(contextualDispatch is null
                ? await dispatch(request.Operation, request.Arguments, ct).ConfigureAwait(false)
                : await contextualDispatch(peer, request, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { return new(null, "FL operation timed out or was cancelled; inspect state before retrying a mutation."); }
        catch (Exception ex)
        {
            log($"[fl-mcp] {request.Operation}: {ex.Message}");
            return new(null, ex.Message);
        }
    }
}
