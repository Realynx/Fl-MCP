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
        Assert.Equal(27, tools.Count);
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
    }

    private static string FindRepository()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "FlMcp.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Could not locate MCP test server.");
    }
}
