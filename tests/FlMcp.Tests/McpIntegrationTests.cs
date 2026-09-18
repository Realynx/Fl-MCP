using FlMcp.Protocol;
using ModelContextProtocol.Client;
using Xunit;

namespace FlMcp.Tests;

public sealed class McpIntegrationTests
{
    [Fact]
    public async Task RealStdioServerInitializesListsToolsAndReturnsActionableSessionError()
    {
        using var files = new TestFiles();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var root = FindRepository();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var dll = Environment.GetEnvironmentVariable("FL_MCP_TEST_SERVER") ??
            Path.Combine(root, "src", "FlMcp.Server", "bin", configuration, "net10.0-windows", "FlMcp.Server.dll");
        var errors = new List<string>();
        var transport = new StdioClientTransport(new()
        {
            Command = "dotnet",
            Arguments = [dll],
            EnvironmentVariables = new Dictionary<string, string?> { [PipeProtocol.WorkspaceVariable] = files.Root },
            StandardErrorLines = line => { lock (errors) errors.Add(line); }
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: deadline.Token);
        var tools = await client.ListToolsAsync(cancellationToken: deadline.Token);
        Assert.Equal(32, tools.Count);
        Assert.Contains(tools, tool => tool.Name == "fl_marker_delete");
        Assert.Contains(tools, tool => tool.Name == "fl_audio_describe");
        Assert.Contains(tools, tool => tool.Name == "fl_section_measure");
        var capture = Assert.Single(tools, tool => tool.Name == "fl_audio_capture");
        var measure = Assert.Single(tools, tool => tool.Name == "fl_section_measure");
        foreach (var description in new[] { capture.Description, measure.Description })
        {
            // The live route configures FL itself now, so the descriptions must say so rather than send the
            // caller to FL's UI (the old text named the Recording filter and Auto-create audio clip as chores).
            Assert.Contains("recording filter", description);
            Assert.Contains("record button", description);
            Assert.Contains("INCLUSIVE", description);
        }
        Assert.Contains("No FL setup is needed", capture.Description);
        Assert.Contains("ensureRecordingFilter=false", capture.Description);
        Assert.DoesNotContain("project is not edited", capture.Description);
        Assert.Contains("retired_channels", capture.Description);
        // A retired channel left pointing at a deleted recording hangs FL's renderer, so both descriptions say
        // the channels are repointed at the placeholder, and that a stem is pre-fader (live 2026-09-17).
        Assert.Contains("repointed_channels", capture.Description);
        Assert.Contains("PRE-fader", capture.Description);
        foreach (var description in new[] { capture.Description, measure.Description })
        {
            Assert.Contains("repointed", description);
            Assert.Contains("fader", description);
        }
        Assert.Contains("sdkMethod", measure.Description);
        var properties = capture.JsonSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("armRefresh", out _));
        Assert.True(properties.TryGetProperty("keepOriginals", out _));
        Assert.True(properties.TryGetProperty("ensureRecordingFilter", out _));
        Assert.True(properties.TryGetProperty("inserts", out _), capture.JsonSchema.GetRawText());
        Assert.True(properties.TryGetProperty("startBar", out _));
        Assert.Contains("startBar", capture.JsonSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain("inserts", capture.JsonSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        var mixer = Assert.Single(tools, tool => tool.Name == "fl_mixer_set");
        Assert.Contains("0..16000", mixer.Description);
        Assert.Contains("12800", mixer.Description);
        Assert.DoesNotContain("no dB conversion", mixer.Description);
        Assert.All(tools, tool => Assert.StartsWith("fl_", tool.Name));
        Assert.Contains(tools, tool => tool.Name == "fl_project_render");
        Assert.Contains(tools, tool => tool.Name == "fl_execute_python");
        Assert.Contains(tools, tool => tool.Name == "fl_instances");
        Assert.Contains(tools, tool => tool.Name == "fl_attach");
        Assert.Contains(tools, tool => tool.Name == "fl_detach");
        var docs = await client.CallToolAsync("fl_python_docs", cancellationToken: deadline.Token);
        Assert.NotEqual(true, docs.IsError);
        var response = await client.CallToolAsync("fl_status", cancellationToken: deadline.Token);
        Assert.True(response.IsError);
        Assert.Contains("No FL project is connected", string.Join(" ", response.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(item => item.Text)));
        TestFiles.WriteWave(files.PathFor("take.wav"));
        var captured = await client.CallToolAsync("fl_audio_capture",
            new Dictionary<string, object?> { ["inserts"] = new[] { 5, 0 }, ["startBar"] = 33, ["endBar"] = 40 }, cancellationToken: deadline.Token);
        Assert.True(captured.IsError);
        Assert.Contains("No FL project is connected", string.Join(" ", captured.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(item => item.Text)));
        var described = await client.CallToolAsync("fl_audio_describe",
            new Dictionary<string, object?> { ["path"] = "take.wav", ["channel"] = 2 }, cancellationToken: deadline.Token);
        Assert.True(described.IsError);
        Assert.Contains("exactly one of path", string.Join(" ", described.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(item => item.Text)));
    }

    private static string FindRepository()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FlMcp.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Could not locate MCP test server.");
    }
}
