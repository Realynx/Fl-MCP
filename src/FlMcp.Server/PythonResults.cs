using System.Globalization;
using System.Text;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>
/// Bounds <c>fl_execute_python</c> responses for the MCP client. The plugin pipe carries up to 1 MiB, but MCP
/// clients cap what a tool result may contain (Claude Code refuses results over roughly 25k tokens, about
/// 80-100 KB of JSON). A response over the limit is written in full to <c>&lt;workspace&gt;/results</c> and replaced
/// by an envelope with the head and tail of that file, its size and its path, so an agent can read the rest
/// with a file tool instead of losing it.
/// </summary>
public static class PythonResults
{
    /// <summary>Default budget in UTF-8 bytes for a response returned inline; override with FL_MCP_PYTHON_RESPONSE_LIMIT.
    /// 64 KiB keeps a clear margin under common client caps and matches the SDK's per-stream capture bound.</summary>
    public const int DefaultLimitBytes = 64 * 1024;

    /// <summary>Smallest accepted limit; the envelope itself needs room for excerpts, the path and the note.</summary>
    public const int MinimumLimitBytes = 4 * 1024;

    public const string LimitVariable = "FL_MCP_PYTHON_RESPONSE_LIMIT";
    public const string ResultsDirectory = "results";

    private static readonly JsonSerializerOptions Indented = new(Messages.Json) { WriteIndented = true };

    /// <summary>Parses the environment override; unparsable values fall back to the default and small values to the minimum.</summary>
    public static int ParseLimit(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(parsed, MinimumLimitBytes)
            : DefaultLimitBytes;

    /// <summary>Returns the response unchanged when it fits, otherwise saves it and returns the summary envelope.
    /// <paramref name="ok"/> overrides the envelope's ok flag for tool results that carry no ok field of their own.</summary>
    public static JsonElement Bound(JsonElement response, int limitBytes, WorkspacePaths workspace, DateTimeOffset now, bool? ok = null)
    {
        limitBytes = Math.Max(limitBytes, MinimumLimitBytes);
        if (Encoding.UTF8.GetByteCount(response.GetRawText()) <= limitBytes) return response;
        var text = JsonSerializer.Serialize(response, Indented);
        var name = $"{now.ToUniversalTime():yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..(16 + 8)] + ".json";
        var path = workspace.NewFile(Path.Combine(ResultsDirectory, name), ".json");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        var totalBytes = Encoding.UTF8.GetByteCount(text);
        var envelope = new Dictionary<string, object?>
        {
            ["ok"] = ok ?? (response.TryGetProperty("ok", out var okFlag) && okFlag.ValueKind == JsonValueKind.True),
            ["oversized"] = true,
            ["totalBytes"] = totalBytes,
            ["limitBytes"] = limitBytes,
            ["path"] = path,
            ["head"] = Head(text, limitBytes / 4),
            ["tail"] = Tail(text, limitBytes / 8),
            ["note"] = $"The response is {totalBytes} bytes, over the {limitBytes}-byte {LimitVariable}. " +
                       (ok is null ? "The complete JSON response {ok,result,stdout,stderr,error?,traceback?} was saved to 'path'; " +
                                     "read it with a file tool, or return a smaller result and print less."
                                   : "The complete JSON result was saved to 'path'; read it with a file tool."),
        };
        if (response.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            envelope["error"] = Head(error.GetString()!, limitBytes / 16);
        foreach (var flag in new[] { "resultPartial", "stdoutTruncated", "stderrTruncated" })
            if (response.TryGetProperty(flag, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                envelope[flag] = value.GetBoolean();
        return Messages.Element(envelope);
    }

    private static string Head(string text, int byteBudget)
    {
        int bytes = 0, index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > byteBudget) break;
            bytes += rune.Utf8SequenceLength;
            index += rune.Utf16SequenceLength;
        }
        return text[..index];
    }

    private static string Tail(string text, int byteBudget)
    {
        int bytes = 0, index = text.Length;
        while (index > 0)
        {
            Rune.DecodeLastFromUtf16(text.AsSpan(0, index), out var rune, out var consumed);
            if (bytes + rune.Utf8SequenceLength > byteBudget) break;
            bytes += rune.Utf8SequenceLength;
            index -= consumed;
        }
        return text[index..];
    }
}
