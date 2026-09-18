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
            return PythonCatalog.Annotate(reply.Result ?? throw new InvalidDataException("SDK catalog was empty."));
        }
        finally { gate.Release(); }
    }

    /// <summary>Longest embedded-Python deadline the plugin pipe holds open.</summary>
    public const int PythonTimeoutCap = 300;

    /// <summary>Executes embedded Python with a deadline CLAMPED to 1..<see cref="PythonTimeoutCap"/> seconds.
    /// A request outside that range used to be refused outright ("Timeout must be 1..300 seconds"), throwing away
    /// the caller's whole script over an argument the server can simply correct (live 2026-09-18:
    /// <c>timeoutSeconds=400</c> was refused). The script now runs at the clamped deadline and the response
    /// carries a <c>warnings</c> entry naming what was asked for and what was used, so nothing changes silently.</summary>
    public async Task<JsonElement> ExecutePythonAsync(string code, int timeoutSeconds, CancellationToken ct)
    {
        var deadline = Math.Clamp(timeoutSeconds, 1, PythonTimeoutCap);
        var clamped = deadline == timeoutSeconds ? null
            : $"TimeoutClamped: timeoutSeconds {timeoutSeconds} is outside 1..{PythonTimeoutCap}; the script ran with a " +
              $"{deadline} s deadline. Work that needs longer has to be split across calls.";
        if (string.IsNullOrWhiteSpace(code) || Encoding.UTF8.GetByteCount(code) > 128 * 1024)
            throw new ArgumentException("Python code must contain 1..131072 UTF-8 bytes.", nameof(code));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            var request = new PythonExecute(code, deadline, expectedProject ?? attachedProject?.Path ?? "") { AttachedProject = attachedProject };
            var response = await CallCoreAsync("python_execute", request, deadline, ct).ConfigureAwait(false);
            // The SDK already returns partial stdout/stderr/result with the traceback on failure; only size is bounded
            // here. The clamp note is added AFTER bounding, so an oversized response cannot bury it in the saved file.
            var bounded = PythonResults.Bound(response, settings.PythonResponseLimitBytes, paths, DateTimeOffset.UtcNow);
            return clamped is null ? bounded : PythonResults.WithWarning(bounded, clamped);
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
