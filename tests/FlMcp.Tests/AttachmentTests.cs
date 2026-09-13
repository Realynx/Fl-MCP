using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FlMcp.Server;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;
using ModelContextProtocol;
using Xunit;

namespace FlMcp.Tests;

public sealed class AttachmentTests
{
    [Fact]
    public async Task AttachUsesConfiguredWorkspaceWithoutTemplateOrOwnedProcessAndDisposesSafely()
    {
        await using var fixture = new Fixture();
        var session = fixture.Session();
        var instances = await session.ListInstancesAsync(CancellationToken.None);
        Assert.Equal(Environment.ProcessId, Assert.Single(instances).ProcessId);
        var attached = await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        Assert.Equal("attached", attached.Ownership);
        Assert.Equal(fixture.Workspace.Root + Path.DirectorySeparatorChar, attached.Workspace);
        Assert.DoesNotContain(fixture.Endpoint.Token, Messages.Element(attached).GetRawText());
        Assert.DoesNotContain("leaseToken", Messages.Element(instances).GetRawText());
        await session.DisposeAsync();
        Assert.Equal(0, fixture.Process.Terminations);
        Assert.Equal(1, fixture.Process.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotPreservesOutsideWorkspaceOrUntitledProjectAndPlayback(bool untitled)
    {
        await using var fixture = new Fixture();
        if (untitled) fixture.Control.Project = new("Untitled", "", true);
        var original = fixture.Control.Project;
        await using var session = fixture.Session();
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        await session.SaveAsync("snapshot.flp", CancellationToken.None);
        Assert.Equal(24, Artifacts.VerifyProject(fixture.Workspace.PathFor("snapshot.flp")));
        Assert.Equal(original, fixture.Control.Project);
        Assert.DoesNotContain("TransportStopAsync", fixture.Control.Calls);
        Assert.DoesNotContain("SetSongModeAsync", fixture.Control.Calls);
        var script = await session.ExecutePythonAsync("result = fl.ops.get_tempo()", 10, CancellationToken.None);
        Assert.Equal(120, script.GetProperty("result").GetDouble());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CloseAsync("close.flp", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RenderAsync("render.wav", 1, CancellationToken.None));
        Assert.False(File.Exists(fixture.Workspace.PathFor("close.flp")));
        Assert.Equal(0, fixture.Process.Terminations);
    }

    [Fact]
    public async Task ProjectSwitchRequiresReattachAndTypedTitleOverridesMisleadingLegacyText()
    {
        await using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        fixture.Control.Project = new("Changed\nPath: misleading", "", true);
        var status = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.True(status.GetProperty("requiresReattach").GetBoolean());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CallAsync("tempo", new TempoArgs(140), CancellationToken.None));
        Assert.DoesNotContain("SetTempoAsync", fixture.Control.Calls);
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        await session.CallAsync("tempo", new TempoArgs(140), CancellationToken.None);
        Assert.Equal(140, fixture.Control.Tempo);
    }

    [Fact]
    public async Task LiveSecondClientIsRefusedUntilDetachAndStaleProcessLeaseCanBeRecovered()
    {
        await using var fixture = new Fixture();
        var first = await fixture.Claim(101);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Claim(102));
        await fixture.Request(101, first.LeaseToken, "detach", new { });
        var second = await fixture.Claim(102);
        fixture.Stamps[102] = null;
        var recovered = await fixture.Claim(101);
        Assert.NotEqual(first.LeaseToken, recovered.LeaseToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Request(102, second.LeaseToken, "tempo", new TempoArgs(150)));
        Assert.DoesNotContain("SetTempoAsync", fixture.Control.Calls);
    }

    [Fact]
    public async Task ReusedPidAndWrongLeaseCannotInvokeOperations()
    {
        await using var fixture = new Fixture();
        var first = await fixture.Claim(101);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Request(101, new string('A', 64), "status", new { }));
        fixture.Stamps[101] = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Request(101, first.LeaseToken, "tempo", new TempoArgs(150)));
        var recovered = await fixture.Claim(101);
        Assert.NotEqual(first.LeaseToken, recovered.LeaseToken);
    }

    [Fact]
    public async Task FailedLeaseReplyCanRetryWithoutEverTerminatingObservedProcess()
    {
        await using var fixture = new Fixture();
        fixture.RewriteReply = reply => reply with { Workspace = "relative" };
        var session = fixture.Session();
        await Assert.ThrowsAsync<InvalidDataException>(() => session.AttachAsync(Environment.ProcessId, CancellationToken.None));
        fixture.RewriteReply = null;
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        await session.DisposeAsync();
        Assert.Equal(0, fixture.Process.Terminations);
        Assert.Equal(2, fixture.Process.Disposals);
    }

    [Fact]
    public async Task DifferentWorkspaceReplyCreatesNoDirectoryAndDisposePreservesFl()
    {
        await using var fixture = new Fixture();
        var unexpected = fixture.Workspace.PathFor("unrequested-workspace");
        fixture.RewriteReply = reply => reply with { Workspace = unexpected };
        var session = fixture.Session();
        await Assert.ThrowsAsync<InvalidDataException>(() => session.AttachAsync(Environment.ProcessId, CancellationToken.None));
        await session.DisposeAsync();
        Assert.False(Directory.Exists(unexpected));
        Assert.Equal(0, fixture.Process.Terminations);
    }

    [Fact]
    public async Task LostConnectionDuringDisposePreservesUserProcessAndStaleLeaseCanRecover()
    {
        await using var fixture = new Fixture();
        var session = fixture.Session();
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        fixture.Disconnected = true;
        await session.DisposeAsync();
        Assert.Equal(0, fixture.Process.Terminations);
        fixture.Stamps[101] = null;
        await fixture.Claim(102);
    }

    [Fact]
    public async Task CancelledAttachedPythonDrainsBeforeDetachWithoutTerminatingFl()
    {
        await using var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.DuringPython = async ct => { entered.TrySetResult(); await release.Task; ct.ThrowIfCancellationRequested(); };
        await using var session = fixture.Session();
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var execution = session.ExecutePythonAsync("unused", 10, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var detached = session.DetachAsync(CancellationToken.None);
        Assert.False(detached.IsCompleted);
        Assert.False(execution.IsCompleted);
        Assert.Equal(0, fixture.Process.Terminations);
        release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        await detached;
        Assert.Equal(0, fixture.Process.Terminations);
    }

    [Fact]
    public async Task AttachedPythonRechecksProjectBeforeDirectCallback()
    {
        await using var fixture = new Fixture();
        fixture.BeforePythonCall = () => fixture.Control.Project = new("Other", "", true);
        await using var session = fixture.Session();
        await session.AttachAsync(Environment.ProcessId, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecutePythonAsync("unused", 10, CancellationToken.None));
        Assert.DoesNotContain("SetTempoAsync", fixture.Control.Calls);
        Assert.Equal(0, fixture.Process.Terminations);
    }

    [Fact]
    public async Task DiscoveryFiltersOtherInstallationAndExplicitAttachRejectsIt()
    {
        await using var fixture = new Fixture();
        fixture.Endpoint = fixture.Endpoint with { ExecutablePath = fixture.Workspace.PathFor("other-FL.exe") };
        await using var session = fixture.Session();
        Assert.Empty(await session.ListInstancesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AttachAsync(Environment.ProcessId, CancellationToken.None));
        Assert.Equal(0, fixture.Process.Disposals);
    }

    [Fact]
    public async Task InvalidInstanceAndRuntimeOverridesDoNotClaimLease()
    {
        await using var fixture = new Fixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Controller.DispatchAsync(101,
            new("", "attach", Messages.Element(new AttachRequest("stale", fixture.Workspace.Root))), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Controller.DispatchAsync(101,
            new("", "attach", Messages.Element(new AttachRequest(fixture.Endpoint.InstanceId, fixture.Workspace.Root, "relative"))), CancellationToken.None));
        await fixture.Claim(102);
    }

    [Fact]
    public async Task ActionableErrorsPreserveGuidanceButRedactTokens()
    {
        var secret = new string('A', 64);
        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(() =>
            throw new IOException("FL exited before readiness. token=" + secret + "; check the host log.")));
        Assert.Contains("check the host log", error.Message);
        Assert.DoesNotContain(secret, error.Message);
        await Assert.ThrowsAsync<NotSupportedException>(() => ToolErrors.Run<int>(() => throw new NotSupportedException("internal")));
    }

    private sealed class Fixture : IAsyncDisposable, IProcessHost, IBridgeClient, IInstanceSource
    {
        public TestFiles Workspace { get; } = new();
        private TestFiles Personal { get; } = new();
        public FakeProcess Process { get; } = new();
        public Dictionary<int, long?> Stamps { get; } = new() { [101] = 1, [102] = 2 };
        public FlInstanceEndpoint Endpoint { get; set; }
        public AttachmentController Controller { get; }
        public ControlProxy Control { get; }
        public Func<AttachReply, AttachReply>? RewriteReply { get; set; }
        public Action? BeforePythonCall { get; set; }
        public Func<CancellationToken, Task>? DuringPython { get; set; }
        public bool Disconnected { get; set; }
        private readonly string executable;

        public Fixture()
        {
            executable = Personal.PathFor("FL64.exe");
            Endpoint = new(Environment.ProcessId, Guid.NewGuid().ToString("D"), new string('B', 64), executable);
            var control = DispatchProxy.Create<ITestControl, ControlProxy>();
            Control = (ControlProxy)control;
            Control.Project = new("Personal", Personal.Project("personal.flp"), false);
            Controller = new(control, Endpoint.InstanceId, executable, pid => Stamps.GetValueOrDefault(pid),
                (paths, _) => new CommandDispatcher(control, paths, handler => new CallbackRuntime(handler,
                    () => BeforePythonCall?.Invoke(), ct => DuringPython?.Invoke(ct) ?? Task.CompletedTask)));
        }

        public ManagedSession Session() => new(new(executable, null, Workspace.Root), this, this, this);
        public Task<FlInstanceEndpoint> ReadAsync(int processId, CancellationToken ct) => Task.FromResult(Endpoint);
        public bool HasRunningStudio() => true;
        public IReadOnlyList<int> ListStudioProcessIds() => [Environment.ProcessId];
        public IManagedProcess Observe(int pid) => Process;
        public IManagedProcess Start(ProcessStartInfo info) => throw new InvalidOperationException("Must not start FL in an attachment test.");
        public async Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
        {
            Assert.Equal(Endpoint.Token, token);
            var reply = await Controller.DispatchAsync(101, new(token, operation, Messages.Element(arguments), timeoutSeconds), ct);
            return RewriteReply is not null ? Messages.Element(RewriteReply(reply.Deserialize<AttachReply>(Messages.Json)!)) : reply;
        }
        public Task<JsonElement> CallAttachedAsync(int processId, string token, string leaseToken, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
        {
            if (Disconnected) throw new IOException("Disconnected test transport.");
            return Request(101, leaseToken, operation, arguments, ct);
        }
        public Task<JsonElement> Request(int peer, string lease, string operation, object args, CancellationToken ct = default) =>
            Controller.DispatchAsync(peer, new("", operation, Messages.Element(args)) { LeaseToken = lease }, ct);
        public async Task<AttachReply> Claim(int peer) => (await Controller.DispatchAsync(peer,
            new("", "attach", Messages.Element(new AttachRequest(Endpoint.InstanceId, Workspace.Root))), CancellationToken.None)).Deserialize<AttachReply>(Messages.Json)!;
        public async ValueTask DisposeAsync() { await Controller.DisposeAsync(); Workspace.Dispose(); Personal.Dispose(); }
    }

    private sealed class CallbackRuntime(Func<string, JsonElement, CancellationToken, Task<object?>> handler, Action before,
        Func<CancellationToken, Task> during) : IEmbeddedPythonRuntime
    {
        public async Task<JsonElement> ExecuteAsync(string code, int timeoutSeconds, CancellationToken ct = default)
        {
            await during(ct);
            before();
            var result = await handler("invoke", Messages.Element(new { operation = "get_tempo", arguments = new { } }), ct);
            return Messages.Element(new { ok = true, result });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeProcess : IManagedProcess
    {
        public int Id => Environment.ProcessId;
        public int Terminations { get; private set; }
        public int Disposals { get; private set; }
        public bool HasExited => false;
        public int ExitCode => 0;
        public Task WaitForExitAsync(CancellationToken ct) => throw new NotSupportedException();
        public void Terminate() => Terminations++;
        public void Dispose() => Disposals++;
    }

    public interface ITestControl : INativeFlControl, IFlStructuredQuery;
    public class ControlProxy : DispatchProxy
    {
        public FlProjectInfo Project { get; set; } = new("", "", true);
        public double Tempo { get; set; } = 120;
        public List<string> Calls { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls.Add(method!.Name);
            return method.Name switch
            {
                "IsAvailableAsync" => Task.FromResult(true),
                "QueryProjectAsync" => Task.FromResult(Project),
                "GetProjectInfoAsync" => Task.FromResult("Title: stale\nPath: misleading\nSaved: no"),
                "GetTempoAsync" => Task.FromResult(Tempo),
                "GetPpqAsync" => Task.FromResult(96),
                "SetTempoAsync" => SetTempo((double)args![0]!),
                "SaveCopyAsync" => Save((string)args![0]!),
                _ => throw new InvalidOperationException("Unexpected fake native call: " + method.Name)
            };
        }
        private Task SetTempo(double value) { Tempo = value; return Task.CompletedTask; }
        private static Task Save(string path) { TestFiles.WriteProject(path); return Task.CompletedTask; }
    }
}
