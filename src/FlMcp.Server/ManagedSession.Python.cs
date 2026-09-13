using System.Text.Json;
using System.Text;
using FlMcp.Protocol;

namespace FlMcp.Server;

public sealed partial class ManagedSession
{
    public async Task<JsonElement> PythonApiAsync(string? filter, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var reply = await PythonCallCoreAsync("catalog", Messages.Element(new { filter }), ct).ConfigureAwait(false);
            if (reply.Error is not null) throw new InvalidOperationException(reply.Error.Value.GetRawText());
            return reply.Result ?? throw new InvalidDataException("SDK catalog was empty.");
        }
        finally { gate.Release(); }
    }

    public async Task<JsonElement> ExecutePythonAsync(string code, int timeoutSeconds, CancellationToken ct)
    {
        ValidateTimeout(timeoutSeconds, 300);
        if (string.IsNullOrWhiteSpace(code) || Encoding.UTF8.GetByteCount(code) > 128 * 1024)
            throw new ArgumentException("Python code must contain 1..131072 UTF-8 bytes.", nameof(code));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            var request = new PythonExecute(code, timeoutSeconds, expectedProject ?? attachedProject?.Path ?? "") { AttachedProject = attachedProject };
            return await CallCoreAsync("python_execute", request, timeoutSeconds, ct).ConfigureAwait(false);
        }
        catch (BridgeCompletionUnknownException) { embeddedCompletionUnknown = true; throw; }
        finally { gate.Release(); }
    }

    private async Task<PythonReply> PythonCallCoreAsync(string method, JsonElement parameters, CancellationToken ct)
    {
        await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
        var result = await CallCoreAsync("python_call", new PythonCall(method, parameters), 60, ct).ConfigureAwait(false);
        return result.Deserialize<PythonReply>(Messages.Json) ?? throw new InvalidDataException("Missing SDK response.");
    }
}
