using System.Reflection;
using System.Text.Json;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;
using Xunit;

namespace FlMcp.Tests;

public sealed class EmbeddedPythonTests
{
    [Fact]
    public async Task RuntimeIsLazyAndCallbackUsesDirectSharedSdkWithoutPipe()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, ProjectControl>();
        var control = (ProjectControl)fl;
        control.Project = files.Project();
        var created = 0;
        FakeRuntime? runtime = null;
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root), handler =>
        {
            created++;
            return runtime = new FakeRuntime(handler);
        });
        await dispatcher.DispatchAsync("python_call", Messages.Element(new PythonCall("catalog", Messages.Element(new { }))), CancellationToken.None);
        Assert.Equal(0, created);
        var result = await dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute("result = fl.ops.get_tempo()", 200, control.Project)), CancellationToken.None);
        Assert.Equal(1, created);
        Assert.True(result.GetProperty("ok").GetBoolean());
        Assert.Equal(123, result.GetProperty("result").GetDouble());
        Assert.Equal(200, runtime!.Timeout);
        Assert.Equal(new[] { "GetProjectInfoAsync", "GetProjectInfoAsync", "GetTempoAsync" }, control.Calls);
        await dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute("result = 2", 1, control.Project)), CancellationToken.None);
        Assert.Equal(1, created);
    }

    [Fact]
    public async Task CallbackRejectsLifecycleChangesBeforeSharedNativeInvocation()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, ProjectControl>();
        var control = (ProjectControl)fl;
        control.Project = files.Project();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root),
            handler => new FakeRuntime(handler) { Operation = "new_project" });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute("unused", 10, control.Project)), CancellationToken.None));
        Assert.Equal(new[] { "GetProjectInfoAsync" }, control.Calls);
    }

    [Fact]
    public async Task ChangedProjectIsRecheckedBeforeEachDirectCallback()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, ProjectControl>();
        var control = (ProjectControl)fl;
        control.Project = files.Project();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root),
            handler => new FakeRuntime(handler) { BeforeCall = () => control.Project = files.PathFor("personal.flp") });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute("unused", 10, control.Project)), CancellationToken.None));
        Assert.DoesNotContain("GetTempoAsync", control.Calls);
    }

    [Fact]
    public async Task WrongInitialProjectNeverConstructsInterpreter()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, ProjectControl>();
        ((ProjectControl)fl).Project = files.Project();
        var created = false;
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root), handler => { created = true; return new FakeRuntime(handler); });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute("unused", 10, files.PathFor("wrong.flp"))), CancellationToken.None));
        Assert.False(created);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("result = 1", 0)]
    [InlineData("result = 1", 301)]
    public async Task InvalidRequestsNeverConstructInterpreter(string code, int seconds)
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, ProjectControl>();
        var created = false;
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root), handler => { created = true; return new FakeRuntime(handler); });
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute(code, seconds, files.PathFor("owned.flp"))), CancellationToken.None));
        Assert.False(created);
        Assert.Empty(((ProjectControl)fl).Calls);
    }

    [Fact]
    public async Task DisposeWaitsForEmbeddedInvocationToDrain()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, ProjectControl>();
        var control = (ProjectControl)fl;
        control.Project = files.Project();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new BlockingRuntime(started, release);
        var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root), _ => runtime);
        var execution = dispatcher.DispatchAsync("python_execute", Messages.Element(new PythonExecute("unused", 10, control.Project)), CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var dispose = dispatcher.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);
        Assert.False(runtime.Disposed);
        release.TrySetResult();
        await execution;
        await dispose;
        Assert.True(runtime.Disposed);
    }

    private sealed class FakeRuntime(Func<string, JsonElement, CancellationToken, Task<object?>> handler) : IEmbeddedPythonRuntime
    {
        public int Timeout { get; private set; }
        public string Operation { get; init; } = "get_tempo";
        public Action? BeforeCall { get; init; }
        public async Task<JsonElement> ExecuteAsync(string code, int timeoutSeconds, CancellationToken ct = default)
        {
            Timeout = timeoutSeconds;
            BeforeCall?.Invoke();
            var value = await handler("invoke", Messages.Element(new { operation = Operation, arguments = new { } }), ct);
            return Messages.Element(new { ok = true, result = value });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class BlockingRuntime(TaskCompletionSource started, TaskCompletionSource release) : IEmbeddedPythonRuntime
    {
        public bool Disposed { get; private set; }
        public async Task<JsonElement> ExecuteAsync(string code, int timeoutSeconds, CancellationToken ct = default)
        {
            started.TrySetResult();
            await release.Task;
            return Messages.Element(new { ok = true });
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    public class ProjectControl : DispatchProxy
    {
        public string Project { get; set; } = "";
        public List<string> Calls { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls.Add(method!.Name);
            return method.Name switch
            {
                "GetProjectInfoAsync" => Task.FromResult("Title: fixture\nPath: " + Project + "\nSaved: yes"),
                "GetTempoAsync" => Task.FromResult(123.0),
                _ => throw new InvalidOperationException("Unexpected fake native call: " + method.Name)
            };
        }
    }
}
