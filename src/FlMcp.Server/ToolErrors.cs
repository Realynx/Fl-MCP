using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace FlMcp.Server;

/// <summary>Expose actionable operation failures through MCP without disclosing private bridge credentials.</summary>
public static partial class ToolErrors
{
    /// <summary>Runs one tool body and turns ANY failure into an <see cref="McpException"/> the caller can read.
    /// Only a cancellation of <paramref name="ct"/> — the MCP request's own token — propagates, because that is the
    /// client withdrawing the call rather than a failure to report. Before 2026-09-17 this converted just five
    /// exception types and every other one reached the MCP SDK as the detail-free "An error occurred invoking
    /// 'fl_plugins_list'." (seen live in an attached session for an operation that worked through Python), which
    /// left the model with nothing to act on.</summary>
    public static async Task<T> Run<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        try { return await action().ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (McpException) { throw; }
        catch (Exception error) { throw new McpException(Describe(error)); }
    }

    /// <summary>The message a tool failure reaches the model as: redacted, never empty, and prefixed with the
    /// exception type unless the type is one whose message already reads as guidance. A cancellation that is NOT
    /// this request's own is named explicitly, because a bare "A task was canceled." explains nothing.</summary>
    internal static string Describe(Exception error)
    {
        if (error is OperationCanceledException)
            return $"{error.GetType().Name}: the operation was cancelled without this request being cancelled — " +
                "FL did not answer inside the server's own deadline. Check FL for a modal dialog or a still-running " +
                "plugin/render operation, confirm the project state, then retry.";
        string message = PrivateToken().Replace(error.Message, "[redacted]");
        if (string.IsNullOrWhiteSpace(message)) message = "(the exception carried no message)";
        return IsActionable(error) ? message : $"{error.GetType().Name}: {message}";
    }

    /// <summary>Failure types whose own message is written for the caller; anything else is an unexpected
    /// failure whose type name is part of the diagnosis.</summary>
    private static bool IsActionable(Exception error) =>
        error is InvalidOperationException or ArgumentException or IOException or InvalidDataException or TimeoutException;

    [GeneratedRegex(@"\b[0-9a-fA-F]{64}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateToken();
}
