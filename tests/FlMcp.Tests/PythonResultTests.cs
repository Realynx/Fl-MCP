using System.Text;
using System.Text.Json;
using FlMcp.Protocol;
using FlMcp.Server;
using Xunit;

namespace FlMcp.Tests;

public sealed class PythonResultTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 9, 14, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void SmallResponsesPassThroughUnchangedAndWriteNothing()
    {
        using var files = new TestFiles();
        var response = Messages.Element(new { ok = true, result = new { tempo = 140.0 }, stdout = "x\n", stderr = "", stdoutTruncated = false, stderrTruncated = false });
        var bounded = PythonResults.Bound(response, PythonResults.MinimumLimitBytes, new WorkspacePaths(files.Root), Stamp);
        Assert.Equal(response.GetRawText(), bounded.GetRawText());
        Assert.False(Directory.Exists(Path.Combine(files.Root, PythonResults.ResultsDirectory)));
    }

    [Fact]
    public void OversizedResponseIsSavedInFullAndSummarizedWithHeadTailAndPath()
    {
        using var files = new TestFiles();
        var rows = Enumerable.Range(0, 400).Select(index => new { index, name = $"Parameter 音 {index}", value = index / 400.0 }).ToArray();
        var response = Messages.Element(new { ok = false, result = rows, resultPartial = true, error = "KeyError: 'late'", traceback = "Traceback...",
            stdout = new string('♫', 3000) + "\n", stderr = "", stdoutTruncated = false, stderrTruncated = true });
        const int limit = 8 * 1024;
        var envelope = PythonResults.Bound(response, limit, new WorkspacePaths(files.Root), Stamp);

        Assert.True(envelope.GetProperty("oversized").GetBoolean());
        Assert.False(envelope.GetProperty("ok").GetBoolean());
        Assert.Equal(limit, envelope.GetProperty("limitBytes").GetInt32());
        Assert.Equal("KeyError: 'late'", envelope.GetProperty("error").GetString());
        Assert.True(envelope.GetProperty("resultPartial").GetBoolean());
        Assert.True(envelope.GetProperty("stderrTruncated").GetBoolean());
        Assert.False(envelope.TryGetProperty("result", out _));
        Assert.Contains(PythonResults.LimitVariable, envelope.GetProperty("note").GetString());

        var path = envelope.GetProperty("path").GetString()!;
        Assert.StartsWith(Path.Combine(files.Root, PythonResults.ResultsDirectory, "20260914-153000-"), path);
        Assert.EndsWith(".json", path);
        var text = File.ReadAllText(path, Encoding.UTF8);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), envelope.GetProperty("totalBytes").GetInt32());
        using var saved = JsonDocument.Parse(text);
        Assert.Equal(JsonSerializer.Serialize(response), JsonSerializer.Serialize(saved.RootElement));

        var head = envelope.GetProperty("head").GetString()!;
        var tail = envelope.GetProperty("tail").GetString()!;
        Assert.StartsWith(head, text);
        Assert.EndsWith(tail, text);
        Assert.InRange(Encoding.UTF8.GetByteCount(head), PythonResults.MaximumHeadBytes - 4, PythonResults.MaximumHeadBytes);
        Assert.InRange(Encoding.UTF8.GetByteCount(tail), PythonResults.MaximumTailBytes - 4, PythonResults.MaximumTailBytes);
        Assert.True(Encoding.UTF8.GetByteCount(envelope.GetRawText()) <= limit, "The envelope itself must fit the limit.");
    }

    [Fact]
    public void ExcerptsAreCappedAtAFewHundredBytesWhateverTheLimitIs()
    {
        // Live 2026-09-17: with the default 64 KiB limit the envelope carried 16 KiB of head and 8 KiB of tail,
        // i.e. several KB of the very blob it was saving to a file. The excerpts only have to identify it.
        using var files = new TestFiles();
        var response = Messages.Element(new { ok = true, result = new string('x', 300_000) });
        var envelope = PythonResults.Bound(response, PythonResults.DefaultLimitBytes, new WorkspacePaths(files.Root), Stamp);
        Assert.True(envelope.GetProperty("totalBytes").GetInt32() > PythonResults.DefaultLimitBytes);
        Assert.InRange(Encoding.UTF8.GetByteCount(envelope.GetProperty("head").GetString()!), 1, PythonResults.MaximumHeadBytes);
        Assert.InRange(Encoding.UTF8.GetByteCount(envelope.GetProperty("tail").GetString()!), 1, PythonResults.MaximumTailBytes);
        Assert.True(Encoding.UTF8.GetByteCount(envelope.GetRawText()) < 4 * 1024, "The envelope must stay small.");
    }

    [Fact]
    public void EachOversizedResponseGetsItsOwnFile()
    {
        using var files = new TestFiles();
        var response = Messages.Element(new { ok = true, result = new string('x', 10_000) });
        var workspace = new WorkspacePaths(files.Root);
        var first = PythonResults.Bound(response, PythonResults.MinimumLimitBytes, workspace, Stamp).GetProperty("path").GetString();
        var second = PythonResults.Bound(response, PythonResults.MinimumLimitBytes, workspace, Stamp).GetProperty("path").GetString();
        Assert.NotEqual(first, second);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(files.Root, PythonResults.ResultsDirectory)).Length);
    }

    [Fact]
    public void ToolResultsWithoutAnOkFieldCanDeclareTheirOwnFlag()
    {
        using var files = new TestFiles();
        var response = Messages.Element(new { method = "live", measurements = new string('m', 10_000) });
        var envelope = PythonResults.Bound(response, PythonResults.MinimumLimitBytes, new WorkspacePaths(files.Root), Stamp, ok: true);
        Assert.True(envelope.GetProperty("oversized").GetBoolean());
        Assert.True(envelope.GetProperty("ok").GetBoolean());
        Assert.Contains("JSON result was saved", envelope.GetProperty("note").GetString());
        Assert.False(PythonResults.Bound(response, PythonResults.MinimumLimitBytes, new WorkspacePaths(files.Root), Stamp).GetProperty("ok").GetBoolean());
    }

    [Theory]
    [InlineData(null, PythonResults.DefaultLimitBytes)]
    [InlineData("", PythonResults.DefaultLimitBytes)]
    [InlineData("lots", PythonResults.DefaultLimitBytes)]
    [InlineData("100", PythonResults.MinimumLimitBytes)]
    [InlineData("200000", 200000)]
    public void LimitParsingFallsBackToDefaultAndClampsToMinimum(string? value, int expected) =>
        Assert.Equal(expected, PythonResults.ParseLimit(value));
}
