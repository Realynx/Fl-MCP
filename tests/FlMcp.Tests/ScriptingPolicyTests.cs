using System.Reflection;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using Xunit;

namespace FlMcp.Tests;

public sealed class ScriptingPolicyTests
{
    [Theory]
    [InlineData("new_project")]
    [InlineData("open_project")]
    [InlineData("save_project")]
    [InlineData("save_project_as")]
    [InlineData("save_new_version")]
    public async Task LifecycleCallsCannotBypassManagedOwnership(string operation)
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, DispatcherTests.RecordingFl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        var response = await dispatcher.DispatchAsync("python_call", Messages.Element(new PythonCall("invoke", Messages.Element(new { operation, arguments = new { } }))), CancellationToken.None);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, response.GetProperty("error").ValueKind);
        Assert.Empty(((DispatcherTests.RecordingFl)fl).Calls);
    }

    [Fact]
    public async Task BatchPolicyRejectsBeforeEarlierMutationRuns()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, DispatcherTests.RecordingFl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        var parameters = Messages.Element(new { operations = new object[] {
            new { operation = "set_tempo", arguments = new { bpm = 123 } },
            new { operation = "save_copy", arguments = new { path = "../outside.flp" } }
        } });
        var response = await dispatcher.DispatchAsync("python_call", Messages.Element(new PythonCall("batch", parameters)), CancellationToken.None);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, response.GetProperty("error").ValueKind);
        Assert.Empty(((DispatcherTests.RecordingFl)fl).Calls);
    }

    [Fact]
    public async Task CatalogComesFromSharedSdkWithoutTouchingNative()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, DispatcherTests.RecordingFl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        var response = await dispatcher.DispatchAsync("python_call", Messages.Element(new PythonCall("catalog", Messages.Element(new { filter = "tempo" }))), CancellationToken.None);
        var entries = response.GetProperty("result").GetProperty("operations").EnumerateArray().ToArray();
        Assert.Contains(entries, item => item.GetProperty("name").GetString() == "set_tempo");
        Assert.Empty(((DispatcherTests.RecordingFl)fl).Calls);
    }

    [Fact]
    public void RepeatedSnapshotPathsInBatchCannotOverwriteEarlierItem()
    {
        using var files = new TestFiles();
        var policy = new ScriptingSessionPolicy(new WorkspacePaths(files.Root));
        var parameters = Messages.Element(new { operations = new[] {
            new { operation = "save_copy", arguments = new { path = "same.flp" } },
            new { operation = "save_copy", arguments = new { path = "SAME.flp" } }
        } });
        Assert.Throws<ArgumentException>(() => policy.Prepare("batch", parameters));
    }
}
