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
        Assert.InRange(Encoding.UTF8.GetByteCount(head), limit / 4 - 4, limit / 4);
        Assert.InRange(Encoding.UTF8.GetByteCount(tail), limit / 8 - 4, limit / 8);
        Assert.True(Encoding.UTF8.GetByteCount(envelope.GetRawText()) <= limit, "The envelope itself must fit the limit.");
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

    [Theory]
    [InlineData(null, PythonResults.DefaultLimitBytes)]
    [InlineData("", PythonResults.DefaultLimitBytes)]
    [InlineData("lots", PythonResults.DefaultLimitBytes)]
    [InlineData("100", PythonResults.MinimumLimitBytes)]
    [InlineData("200000", 200000)]
    public void LimitParsingFallsBackToDefaultAndClampsToMinimum(string? value, int expected) =>
        Assert.Equal(expected, PythonResults.ParseLimit(value));
}
