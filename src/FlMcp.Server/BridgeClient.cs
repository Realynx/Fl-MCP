using System.IO.Pipes;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

public interface IBridgeClient
{
    Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct);
}

public sealed class BridgeClient : IBridgeClient
{
    public async Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 2));
        await using var pipe = new NamedPipeClientStream(".", PipeProtocol.Name(processId), PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
        await PipeProtocol.WriteAsync(pipe, new BridgeRequest(token, operation, Messages.Element(arguments), timeoutSeconds), deadline.Token).ConfigureAwait(false);
        var response = await PipeProtocol.ReadAsync<BridgeResponse>(pipe, deadline.Token).ConfigureAwait(false);
        if (response.Error is not null) throw new InvalidOperationException(response.Error);
        return response.Result ?? throw new InvalidDataException("Bridge returned no result.");
    }
}
