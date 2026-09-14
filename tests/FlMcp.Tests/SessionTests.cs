using System.Diagnostics;
using System.Text.Json;
using FlMcp.Protocol;
using FlMcp.Server;
using Xunit;

namespace FlMcp.Tests;

public sealed class SessionTests
{
    [Fact]
    public async Task RefusesExistingStudioWithoutStartingOrStoppingIt()
    {
        using var fixture = new Fixture();
        fixture.Processes.ExistingStudio = true;
        await using var session = fixture.Session();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LaunchAsync("fresh.flp", 1, CancellationToken.None));
        Assert.Empty(fixture.Processes.Started);
    }

    [Fact]
    public async Task BackgroundSessionCanLaunchBesideOtherStudioProcesses()
    {
        using var fixture = new Fixture();
        fixture.Processes.ExistingStudio = true;
        await using var session = fixture.Session();
        await session.LaunchAsync("background.flp", 1, CancellationToken.None, background: true);

        var launched = Assert.Single(fixture.Processes.Started);
        Assert.True(launched.Background);
        Assert.Equal(1, launched.Process.StartupCompletions);
    }

    [Fact]
    public async Task BackgroundSessionKeepsRenderOnPrivateDesktop()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("background.flp", 1, CancellationToken.None, background: true);
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("background.wav"));

        await session.RenderAsync("background.wav", 1, CancellationToken.None);

        Assert.Equal(2, fixture.Processes.Started.Count);
        Assert.All(fixture.Processes.Started, launched => Assert.True(launched.Background));
    }

    [Fact]
    public async Task BackgroundStartupGateUsesProjectDeadline()
    {
        using var fixture = new Fixture();
        fixture.Processes.BlockStart = true;
        await using var session = fixture.Session();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            session.LaunchAsync("background.flp", 1, CancellationToken.None, background: true));

        Assert.Empty(fixture.Processes.Started);
    }

    [Fact]
    public async Task SecondLaunchDoesNotStopTheAlreadyManagedSession()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LaunchAsync("other.flp", 1, CancellationToken.None));
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
        var status = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.True(status.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task ReadinessTimeoutStopsOnlyOwnedProcess()
    {
        using var fixture = new Fixture();
        fixture.Bridge.Available = false;
        await using var session = fixture.Session();
        await Assert.ThrowsAsync<TimeoutException>(() => session.LaunchAsync("fresh.flp", 1, CancellationToken.None));
        var launched = Assert.Single(fixture.Processes.Started).Process;
        Assert.True(launched.Terminated);
        Assert.Equal(0, launched.StartupCompletions);
    }

    [Fact]
    public async Task SaveFailurePreservesEditingProcessAndDoesNotRender()
    {
        using var fixture = new Fixture();
        fixture.Bridge.ValidSave = false;
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.RenderAsync("mix.wav", 1, CancellationToken.None));
        Assert.False(Assert.Single(fixture.Processes.Started).Process.Terminated);
    }

    [Fact]
    public async Task RenderSnapshotsBeforeStoppingEditorAndUsesDocumentedArguments()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Processes.OnRenderExit = info => TestFiles.WriteWave(fixture.Files.PathFor("finished mix.wav"));
        var result = Messages.Element(await session.RenderAsync("finished mix.wav", 1, CancellationToken.None));
        Assert.True(fixture.Processes.Started[0].Process.Terminated);
        var render = fixture.Processes.Started[1].Info;
        Assert.Equal(new[] { "/R", "/Ewav", "/O" + fixture.Files.Root }, render.ArgumentList.Take(3));
        Assert.EndsWith("finished mix.flp", render.ArgumentList[3]);
        Assert.False(render.Environment.ContainsKey(PipeProtocol.TokenVariable));
        Assert.False(render.UseShellExecute);
        Assert.Equal(ProcessWindowStyle.Hidden, render.WindowStyle);
        Assert.True(result.GetProperty("sessionClosed").GetBoolean());
        Assert.True(File.Exists(result.GetProperty("project").GetString()));
        Assert.Equal(48, result.GetProperty("bytes").GetInt64());
    }

    [Fact]
    public async Task CancelledRenderTerminatesItsOwnedRendererAndPreservesSnapshot()
    {
        using var fixture = new Fixture();
        fixture.Processes.HangRender = true;
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        var error = await Assert.ThrowsAsync<IOException>(() => session.RenderAsync("mix.wav", 1, CancellationToken.None));
        Assert.Contains("Snapshot preserved at", error.Message);
        Assert.True(fixture.Processes.Started[1].Process.Terminated);
        Assert.True(File.Exists(fixture.Processes.Started[1].Info.ArgumentList[3]));
    }

    [Fact]
    public async Task LaunchUsesPrivateTokenAndTemplateCopy()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("new project.flp", 1, CancellationToken.None);
        var launch = Assert.Single(fixture.Processes.Started).Info;
        Assert.Equal(fixture.Files.PathFor("new project.flp"), Assert.Single(launch.ArgumentList));
        Assert.Equal(64, launch.Environment[PipeProtocol.TokenVariable]!.Length);
        Assert.NotEqual(fixture.Settings.Template, launch.ArgumentList[0]);
        Assert.Equal(File.ReadAllBytes(fixture.Settings.Template!), File.ReadAllBytes(launch.ArgumentList[0]));
        Assert.Equal(fixture.Settings.ResolvePythonRuntime(), launch.Environment["FL_MCP_PYTHON_RUNTIME"]);
        Assert.Equal(fixture.Settings.ResolvePythonPackage(), launch.Environment["FL_MCP_PYTHON_PATH"]);
        Assert.False(launch.Environment.ContainsKey("FL_MCP_PYTHON"));
    }

    [Fact]
    public async Task ConcurrentReaderDoesNotBlockSharedTemplateValidationAndCopy()
    {
        using var fixture = new Fixture();
        using var concurrentReader = new FileStream(fixture.Settings.Template!, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        await using var session = fixture.Session();

        await session.LaunchAsync("shared-template-copy.flp", 1, CancellationToken.None);

        var copied = Assert.Single(fixture.Processes.Started).Info.ArgumentList[0];
        Assert.Equal(File.ReadAllBytes(fixture.Settings.Template!), File.ReadAllBytes(copied));
    }

    private sealed class Fixture : IDisposable
    {
        public TestFiles Files { get; } = new();
        public FakeProcesses Processes { get; } = new();
        public FakeBridge Bridge { get; } = new();
        public ServerSettings Settings { get; }
        public Fixture()
        {
            var executable = Files.PathFor("FL64.exe");
            File.WriteAllText(executable, "test-only placeholder; never executed");
            Settings = new(executable, Files.Project(), Files.Root);
            Processes.OnStart = info => Bridge.Project = info.ArgumentList[0];
        }
        public ManagedSession Session() => new(Settings, Processes, Bridge);
        public void Dispose() => Files.Dispose();
    }

    [Fact]
    public async Task WrongProjectCannotSatisfyReadiness()
    {
        using var fixture = new Fixture();
        fixture.Processes.OnStart = _ => fixture.Bridge.Project = fixture.Files.PathFor("wrong.flp");
        await using var session = fixture.Session();
        await Assert.ThrowsAsync<TimeoutException>(() => session.LaunchAsync("fresh.flp", 1, CancellationToken.None));
        Assert.True(Assert.Single(fixture.Processes.Started).Process.Terminated);
    }

    [Fact]
    public async Task ChangedProjectRejectsMutationAndSave()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.Project = fixture.Files.PathFor("personal.flp");
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CallAsync("tempo", new TempoArgs(90), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveAsync("saved.flp", CancellationToken.None));
        Assert.DoesNotContain("tempo", fixture.Bridge.Calls);
        Assert.DoesNotContain("save", fixture.Bridge.Calls);
    }

    [Fact]
    public async Task CancelledRenderCanResumePreservedSnapshotAndRenderAgain()
    {
        using var fixture = new Fixture();
        fixture.Processes.HangRender = true;
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => session.RenderAsync("mix.wav", 1, CancellationToken.None));
        var snapshot = fixture.Processes.Started[1].Info.ArgumentList[3];
        fixture.Processes.HangRender = false;
        await session.LaunchAsync("resumed.flp", 1, CancellationToken.None, snapshot);
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("retry.wav"));
        var result = Messages.Element(await session.RenderAsync("retry.wav", 1, CancellationToken.None));
        Assert.Equal(48, result.GetProperty("bytes").GetInt64());
    }

    [Fact]
    public async Task CloseSavesBeforeEndingSession()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        await session.CloseAsync("finished.flp", CancellationToken.None);
        Assert.True(Assert.Single(fixture.Processes.Started).Process.Terminated);
        Assert.Equal(24, Artifacts.VerifyProject(fixture.Files.PathFor("finished.flp")));
    }

    [Fact]
    public async Task EmbeddedExecutionRoutesOneBridgeRequestAndBlocksConcurrentEdits()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Bridge.OnPython = async (request, ct) =>
        {
            Assert.Equal("result = 123", request.Code);
            Assert.Equal(200, request.TimeoutSeconds);
            Assert.Equal(fixture.Files.PathFor("fresh.flp"), request.ExpectedProjectPath);
            called.TrySetResult();
            await release.Task.WaitAsync(ct);
            return Messages.Element(new { ok = true, result = 123 });
        };
        var execution = session.ExecutePythonAsync("result = 123", 200, CancellationToken.None);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var edit = session.CallAsync("tempo", new TempoArgs(123), CancellationToken.None);
        Assert.False(edit.IsCompleted);
        release.TrySetResult();
        await execution;
        await edit;
        Assert.Single(fixture.Bridge.Calls, operation => operation == "python_execute");
        Assert.DoesNotContain("python_call", fixture.Bridge.Calls);
    }

    [Fact]
    public async Task OversizedEmbeddedResponseIsSavedUnderWorkspaceAndSummarized()
    {
        using var fixture = new Fixture();
        await using var session = new ManagedSession(fixture.Settings with { PythonResponseLimitBytes = PythonResults.MinimumLimitBytes }, fixture.Processes, fixture.Bridge);
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Messages.Element(new { ok = true, result = new string('r', 20_000), stdout = "kept\n", stderr = "", stdoutTruncated = false, stderrTruncated = false }));
        var envelope = await session.ExecutePythonAsync("result = 'r' * 20000", 10, CancellationToken.None);
        Assert.True(envelope.GetProperty("oversized").GetBoolean());
        Assert.True(envelope.GetProperty("ok").GetBoolean());
        var path = envelope.GetProperty("path").GetString()!;
        Assert.StartsWith(Path.Combine(fixture.Files.Root, PythonResults.ResultsDirectory) + Path.DirectorySeparatorChar, path);
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(20_000, saved.RootElement.GetProperty("result").GetString()!.Length);
        Assert.Equal("kept\n", saved.RootElement.GetProperty("stdout").GetString());

        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Messages.Element(new { ok = false, result = new { tempo = 120 }, resultPartial = true, error = "ValueError: late", stdout = "before\n" }));
        var failure = await session.ExecutePythonAsync("raise ValueError", 10, CancellationToken.None);
        Assert.False(failure.GetProperty("ok").GetBoolean());
        Assert.Equal(120, failure.GetProperty("result").GetProperty("tempo").GetInt32());
        Assert.Equal("before\n", failure.GetProperty("stdout").GetString());
        Assert.False(failure.TryGetProperty("oversized", out _));
    }

    [Fact]
    public async Task TypedProjectIdentityOverridesMisleadingLegacyText()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.StructuredProject = fixture.Files.PathFor("different.flp");
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PythonApiAsync(null, CancellationToken.None));
        Assert.DoesNotContain("python_call", fixture.Bridge.Calls);
    }

    [Fact]
    public async Task CancelledEmbeddedInvocationMustDrainBeforeCloseCanStopStudio()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Bridge.OnPython = async (_, ct) =>
        {
            using var registration = ct.Register(() => cancelled.TrySetResult());
            called.TrySetResult();
            await release.Task;
            ct.ThrowIfCancellationRequested();
            return Messages.Element(new { ok = true });
        };
        using var cancellation = new CancellationTokenSource();
        var execution = session.ExecutePythonAsync("result = 1", 10, cancellation.Token);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var closing = session.CloseAsync("saved.flp", CancellationToken.None);
        Assert.False(execution.IsCompleted);
        Assert.False(closing.IsCompleted);
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
        release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        await closing;
        Assert.True(fixture.Processes.Started[0].Process.Terminated);
    }

    [Fact]
    public async Task LostEmbeddedAcknowledgementPreventsDisposeFromKillingFlUntilStatusConfirmsIdle()
    {
        using var fixture = new Fixture();
        var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => throw new BridgeCompletionUnknownException(new EndOfStreamException());
        await Assert.ThrowsAsync<BridgeCompletionUnknownException>(() => session.ExecutePythonAsync("result = 1", 10, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.DisposeAsync().AsTask());
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
        await session.CallAsync("status", new { }, CancellationToken.None);
        await session.DisposeAsync();
        Assert.True(fixture.Processes.Started[0].Process.Terminated);
    }

    [Fact]
    public async Task RenderRangePreservesTheFullProjectThenIsolatesBarsBeforeTheRenderSnapshot()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        PythonExecute? request = null;
        fixture.Bridge.OnPython = (execute, _) =>
        {
            request = execute;
            return Task.FromResult(Messages.Element(new { ok = true, result = new { kept_clips = 2, cut_clips = 0 } }));
        };
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("section.wav"));

        var result = Messages.Element(await session.RenderAsync("section.wav", 1, CancellationToken.None, new RenderRange(49, 64)));

        Assert.Contains("from fruitylink.audition import isolate_bars", request!.Code);
        Assert.Contains("isolate_bars(fl, 49, 64, cut_clips=False)", request.Code);
        Assert.Equal(new[] { "save", "python_execute", "save" }, fixture.Bridge.Calls.Where(call => call != "status"));
        var fullProject = result.GetProperty("fullProject").GetString()!;
        Assert.EndsWith("section-full.flp", fullProject);
        Assert.True(File.Exists(fullProject));
        Assert.EndsWith("section.flp", result.GetProperty("project").GetString());
        Assert.Equal(Path.GetDirectoryName(fullProject), Path.GetDirectoryName(result.GetProperty("project").GetString()));
        Assert.Equal(2, result.GetProperty("range").GetProperty("kept_clips").GetInt32());
        Assert.True(result.GetProperty("sessionClosed").GetBoolean());
        Assert.EndsWith("section.flp", fixture.Processes.Started[1].Info.ArgumentList[3]);
    }

    [Fact]
    public async Task FailedRangeIsolationKeepsTheEditorOpenAndNeverStartsTheRenderer()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Messages.Element(
            new { ok = false, result = (object?)null, error = "ValueError: Clips [3] begin before tick 18432 and would be cut" }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.RenderAsync("section.wav", 1, CancellationToken.None, new RenderRange(49, 64, CutClips: true)));

        Assert.Contains("Clips [3] begin before tick 18432", error.Message);
        Assert.Contains("section-full.flp", error.Message);
        Assert.Single(fixture.Processes.Started);
        Assert.False(fixture.Processes.Started[0].Process.Terminated);
        Assert.Equal(new[] { "save", "python_execute" }, fixture.Bridge.Calls.Where(call => call != "status"));
        var status = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.True(status.GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task RenderWithoutRangeDoesNotTouchPythonOrSaveAFullSnapshot()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => throw new InvalidOperationException("no Python expected");
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("plain.wav"));

        var result = Messages.Element(await session.RenderAsync("plain.wav", 1, CancellationToken.None));

        Assert.Equal(new[] { "save" }, fixture.Bridge.Calls.Where(call => call != "status"));
        Assert.Equal(JsonValueKind.Null, result.GetProperty("fullProject").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("range").ValueKind);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(5, 4)]
    public void RenderRangeRejectsInvalidBars(int startBar, int endBar) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderRange.From(startBar, endBar, false));

    [Fact]
    public void RenderRangeRequiresBothBarsOrNeither()
    {
        Assert.Null(RenderRange.From(null, null, false));
        Assert.Throws<ArgumentException>(() => RenderRange.From(49, null, false));
        Assert.Throws<ArgumentException>(() => RenderRange.From(null, 64, false));
        Assert.Equal(new RenderRange(49, 64, true), RenderRange.From(49, 64, true));
        Assert.Equal(new RenderRange(49, 64, false, 8), RenderRange.From(49, 64, false, 8));
        Assert.Throws<ArgumentException>(() => RenderRange.From(null, null, false, 8));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RenderRangeRejectsInvalidTails(double tailBeats) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderRange.From(49, 64, false, tailBeats));

    [Fact]
    public void RenderRangePassesTailBeatsToIsolateBarsOnlyWhenSet()
    {
        Assert.Equal("49, 64, cut_clips=False", new RenderRange(49, 64).PythonArguments());
        Assert.Equal("49, 64, cut_clips=True, tail_beats=8", new RenderRange(49, 64, true, 8).PythonArguments());
        Assert.Equal("1, 4, cut_clips=False, tail_beats=2.5", new RenderRange(1, 4, false, 2.5).PythonArguments());
    }

    [Fact]
    public async Task RenderRangeWithTailAsksTheSdkForAnEndMarkerPastTheSpan()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        PythonExecute? request = null;
        fixture.Bridge.OnPython = (execute, _) =>
        {
            request = execute;
            return Task.FromResult(Messages.Element(new { ok = true, result = new { kept_clips = 2, tail_ticks = 768 } }));
        };
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("tailed.wav"));

        var result = Messages.Element(await session.RenderAsync("tailed.wav", 1, CancellationToken.None, new RenderRange(49, 64, false, 8)));

        Assert.Contains("isolate_bars(fl, 49, 64, cut_clips=False, tail_beats=8)", request!.Code);
        Assert.Equal(768, result.GetProperty("range").GetProperty("tail_ticks").GetInt32());
    }

    [Fact]
    public async Task ExitedUnconfirmedSessionDoesNotPreventFailedNewLaunchCleanup()
    {
        using var fixture = new Fixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => throw new BridgeCompletionUnknownException(new EndOfStreamException());
        await Assert.ThrowsAsync<BridgeCompletionUnknownException>(() => session.ExecutePythonAsync("result = 1", 10, CancellationToken.None));
        fixture.Processes.Started[0].Process.Terminate();
        fixture.Bridge.Available = false;
        await Assert.ThrowsAsync<TimeoutException>(() => session.LaunchAsync("new-generation.flp", 1, CancellationToken.None));
        Assert.True(fixture.Processes.Started[1].Process.Terminated);
    }

    private sealed class FakeBridge : IBridgeClient
    {
        public bool Available { get; set; } = true;
        public bool ValidSave { get; set; } = true;
        public string Project { get; set; } = "";
        public string? StructuredProject { get; set; }
        public List<string> Calls { get; } = [];
        public Func<PythonExecute, CancellationToken, Task<JsonElement>>? OnPython { get; set; }
        public Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add(operation);
            if (operation == "python_execute") return OnPython!((PythonExecute)arguments, ct);
            if (operation == "python_call") return Task.FromResult(Messages.Element(new PythonReply(Messages.Element(new { apiVersion = 1, operations = Array.Empty<object>() }))));
            if (operation == "save")
            {
                var path = ((PathArgs)arguments).Path;
                if (ValidSave) TestFiles.WriteProject(path);
                else File.WriteAllText(path, "this is not a valid project file");
                return Task.FromResult(Messages.Element(new { path }));
            }
            return Task.FromResult(Messages.Element(new SessionStatus(Available, processId, "Title: fixture\nPath: " + Project + "\nSaved: yes", 120, 96) { ProjectPath = StructuredProject }));
        }
    }

    private sealed class FakeProcesses : IProcessHost
    {
        public bool ExistingStudio { get; set; }
        public bool HangRender { get; set; }
        public bool BlockStart { get; set; }
        public Action<ProcessStartInfo>? OnRenderExit { get; set; }
        public Action<ProcessStartInfo>? OnStart { get; set; }
        public List<(ProcessStartInfo Info, FakeProcess Process, bool Background)> Started { get; } = [];
        public bool HasRunningStudio() => ExistingStudio;
        public IManagedProcess Start(ProcessStartInfo info, bool background = false, CancellationToken ct = default)
        {
            if (BlockStart)
            {
                ct.WaitHandle.WaitOne();
                ct.ThrowIfCancellationRequested();
            }
            OnStart?.Invoke(info);
            var process = new FakeProcess(100 + Started.Count, async ct =>
            {
                if (HangRender) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                OnRenderExit?.Invoke(info);
            });
            Started.Add((info, process, background));
            return process;
        }
    }

    private sealed class FakeProcess(int id, Func<CancellationToken, Task> wait) : IManagedProcess
    {
        public int Id => id;
        public bool Terminated { get; private set; }
        public int StartupCompletions { get; private set; }
        public bool HasExited => Terminated;
        public int ExitCode => 0;
        public Task WaitForExitAsync(CancellationToken ct) => wait(ct);
        public void CompleteStartup() => StartupCompletions++;
        public void Terminate() => Terminated = true;
        public void Dispose() { }
    }
}
