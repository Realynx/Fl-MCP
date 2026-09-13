using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace FlMcp.Server;

/// <summary>Expose actionable operation failures through MCP without disclosing private bridge credentials.</summary>
public static partial class ToolErrors
{
    public static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try { return await action().ConfigureAwait(false); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IOException or InvalidDataException or TimeoutException)
        {
            throw new McpException(PrivateToken().Replace(error.Message, "[redacted]"));
        }
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{64}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateToken();
}
