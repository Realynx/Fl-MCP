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
        using var fixture = new SessionFixture();
        fixture.Processes.ExistingStudio = true;
        await using var session = fixture.Session();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LaunchAsync("fresh.flp", 1, CancellationToken.None));
        Assert.Empty(fixture.Processes.Started);
    }

    [Fact]
    public async Task BackgroundSessionCanLaunchBesideOtherStudioProcesses()
    {
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
        fixture.Processes.BlockStart = true;
        await using var session = fixture.Session();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            session.LaunchAsync("background.flp", 1, CancellationToken.None, background: true));

        Assert.Empty(fixture.Processes.Started);
    }

    [Fact]
    public async Task SecondLaunchDoesNotStopTheAlreadyManagedSession()
    {
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
        fixture.Bridge.ValidSave = false;
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.RenderAsync("mix.wav", 1, CancellationToken.None));
        Assert.False(Assert.Single(fixture.Processes.Started).Process.Terminated);
    }

    [Fact]
    public async Task RenderSnapshotsBeforeStoppingEditorAndUsesDocumentedArguments()
    {
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
        using var concurrentReader = new FileStream(fixture.Settings.Template!, FileMode.Open, FileAccess.Read,
            FileShare.Read);
        await using var session = fixture.Session();

        await session.LaunchAsync("shared-template-copy.flp", 1, CancellationToken.None);

        var copied = Assert.Single(fixture.Processes.Started).Info.ArgumentList[0];
        Assert.Equal(File.ReadAllBytes(fixture.Settings.Template!), File.ReadAllBytes(copied));
    }

    [Fact]
    public async Task WrongProjectCannotSatisfyReadiness()
    {
        using var fixture = new SessionFixture();
        fixture.Processes.OnStart = _ => fixture.Bridge.Project = fixture.Files.PathFor("wrong.flp");
        await using var session = fixture.Session();
        await Assert.ThrowsAsync<TimeoutException>(() => session.LaunchAsync("fresh.flp", 1, CancellationToken.None));
        Assert.True(Assert.Single(fixture.Processes.Started).Process.Terminated);
    }

    [Fact]
    public async Task ChangedProjectRejectsMutationAndSave()
    {
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        await session.CloseAsync("finished.flp", CancellationToken.None);
        Assert.True(Assert.Single(fixture.Processes.Started).Process.Terminated);
        Assert.Equal(24, Artifacts.VerifyProject(fixture.Files.PathFor("finished.flp")));
    }

    /// <summary>Live finding 2026-09-18: fl_execute_python(timeoutSeconds=400) was refused with
    /// "Timeout must be 1..300 seconds", losing a whole script over a number the server can correct.
    /// It is clamped now, and the response says so in warnings.</summary>
    [Theory]
    [InlineData(400, 300)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task AnOutOfRangePythonDeadlineIsClampedAndReportedInWarnings(int asked, int used)
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        var seen = 0;
        fixture.Bridge.OnPython = (request, _) =>
        {
            seen = request.TimeoutSeconds;
            return Task.FromResult(Messages.Element(new { ok = true, result = 7 }));
        };

        var response = await session.ExecutePythonAsync("result = 7", asked, CancellationToken.None);

        Assert.Equal(used, seen);
        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(7, response.GetProperty("result").GetInt32());
        var warning = Assert.Single(response.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.StartsWith("TimeoutClamped:", warning);
        Assert.Contains(asked.ToString(), warning);
        Assert.Contains($"{used} s deadline", warning);
    }

    [Fact]
    public async Task AnInRangePythonDeadlineIsUsedVerbatimAndAddsNoWarning()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        var seen = 0;
        fixture.Bridge.OnPython = (request, _) =>
        {
            seen = request.TimeoutSeconds;
            return Task.FromResult(Messages.Element(new { ok = true, result = 7 }));
        };

        var response = await session.ExecutePythonAsync("result = 7", ManagedSession.PythonTimeoutCap, CancellationToken.None);

        Assert.Equal(ManagedSession.PythonTimeoutCap, seen);
        Assert.False(response.TryGetProperty("warnings", out _));
    }

    [Fact]
    public async Task AClampedDeadlineStillWarnsWhenTheResponseIsOversized()
    {
        using var fixture = new SessionFixture();
        await using var session = new ManagedSession(fixture.Settings with { PythonResponseLimitBytes = PythonResults.MinimumLimitBytes },
            fixture.Processes, fixture.Bridge);
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => Task.FromResult(Messages.Element(new { ok = true, result = new string('r', 20_000) }));

        var envelope = await session.ExecutePythonAsync("result = 'r' * 20000", 400, CancellationToken.None);

        // The note has to survive the oversize envelope, not end up inside the file the envelope points at.
        Assert.True(envelope.GetProperty("oversized").GetBoolean());
        Assert.StartsWith("TimeoutClamped:", Assert.Single(envelope.GetProperty("warnings").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task EmbeddedExecutionRoutesOneBridgeRequestAndBlocksConcurrentEdits()
    {
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.StructuredProject = fixture.Files.PathFor("different.flp");
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PythonApiAsync(null, CancellationToken.None));
        Assert.DoesNotContain("python_call", fixture.Bridge.Calls);
    }

    [Fact]
    public async Task CancelledEmbeddedInvocationMustDrainBeforeCloseCanStopStudio()
    {
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
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
        Assert.Equal(new[] { "save", "python_execute", "save", "song" }, fixture.Bridge.Calls.Where(call => call != "status"));
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
        using var fixture = new SessionFixture();
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
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => throw new InvalidOperationException("no Python expected");
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("plain.wav"));

        var result = Messages.Element(await session.RenderAsync("plain.wav", 1, CancellationToken.None));

        Assert.Equal(new[] { "save", "song" }, fixture.Bridge.Calls.Where(call => call != "status"));
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
        using var fixture = new SessionFixture();
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
    public async Task LaunchReportsTheSettledTempoRatherThanTheTemplateTempo()
    {
        using var fixture = new SessionFixture();
        foreach (var tempo in new double[] { 140, 140, 100 }) fixture.Bridge.TempoSequence.Enqueue(tempo);
        await using var session = fixture.Session();

        var status = await session.LaunchAsync("resumed.flp", 5, CancellationToken.None);

        Assert.Equal(100, status.Tempo);
        Assert.NotNull(status.Settle);
        Assert.True(status.Settle!.Stable);
        Assert.Equal(140, status.Settle.FirstTempo);
        Assert.True(status.Settle.Polls >= 2);
        Assert.Empty(status.Warnings);
        var later = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.Equal(100, later.GetProperty("tempo").GetDouble());
    }

    [Fact]
    public async Task UnsettledProjectReturnsTheLastValuesWithAWarningAfterTheBoundedWait()
    {
        using var fixture = new SessionFixture();
        await using var session = new ManagedSession(fixture.Settings with { ProjectSettleTimeout = TimeSpan.FromMilliseconds(60) }, fixture.Processes, fixture.Bridge);
        for (var i = 0; i < 400; i++) fixture.Bridge.TempoSequence.Enqueue(i % 2 == 0 ? 140 : 100);

        var status = await session.LaunchAsync("flapping.flp", 5, CancellationToken.None);

        Assert.False(status.Settle!.Stable);
        Assert.True(status.Settle.Milliseconds >= 60);
        var warning = Assert.Single(status.Warnings);
        Assert.Equal("ProjectUnsettled", warning.Code);
        Assert.Contains("fl_status", warning.Message);
    }

    [Fact]
    public async Task CrashedRenderMovesThePartialAsideAndRetriesOnceFromTheSameSnapshot()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        fixture.Bridge.Song = new SongExtent(41472, 41472, 0, 100, 96, 259.2);
        fixture.Processes.ExitCodes.Enqueue(250477278);
        fixture.Processes.ExitCodes.Enqueue(0);
        var renders = 0;
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("song.wav"), ++renders == 1 ? 94.3 : 259.2);

        var result = Messages.Element(await session.RenderAsync("song.wav", 5, CancellationToken.None));

        Assert.Equal(3, fixture.Processes.Started.Count);
        Assert.Equal(fixture.Processes.Started[1].Info.ArgumentList[3], fixture.Processes.Started[2].Info.ArgumentList[3]);
        Assert.True(fixture.Processes.Started[1].Process.Terminated);
        Assert.Equal(259.2, result.GetProperty("seconds").GetDouble(), 3);
        Assert.Equal(259.2, result.GetProperty("expectedSeconds").GetDouble(), 3);
        var attempts = result.GetProperty("attempts").EnumerateArray().ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.Equal("failed", attempts[0].GetProperty("outcome").GetString());
        Assert.Equal(250477278, attempts[0].GetProperty("exitCode").GetInt32());
        Assert.Equal(94.3, attempts[0].GetProperty("seconds").GetDouble(), 3);
        var partial = attempts[0].GetProperty("partialPath").GetString()!;
        Assert.EndsWith("song.failed-attempt1.wav", partial);
        Assert.True(File.Exists(partial));
        Assert.Equal("ok", attempts[1].GetProperty("outcome").GetString());
        Assert.Equal(259.2, Artifacts.ReadWave(result.GetProperty("path").GetString()!).Seconds, 3);
        var codes = result.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetProperty("code").GetString()).ToArray();
        Assert.Equal(new[] { "RenderRetried" }, codes);
    }

    [Fact]
    public async Task RenderFailingTwiceReportsBothAttemptsAndKeepsTheSnapshot()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        fixture.Bridge.Song = new SongExtent(41472, 41472, 0, 100, 96, 259.2);
        fixture.Processes.ExitCodes.Enqueue(250477278);
        fixture.Processes.ExitCodes.Enqueue(7);
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("song.wav"), 94.3);
        File.WriteAllText(fixture.Files.PathFor($"plugin-host-{DateTime.Now:yyyyMMdd}.log"), "one\ntwo\nplugin X faulted\n");

        var error = await Assert.ThrowsAsync<IOException>(() => session.RenderAsync("song.wav", 5, CancellationToken.None));

        Assert.Contains("Render failed 2 times", error.Message);
        Assert.Contains("Attempt 1: FL render exited with code 250477278", error.Message);
        Assert.Contains("Attempt 2: FL render exited with code 7", error.Message);
        Assert.Contains("WAV 94.3 s", error.Message);
        Assert.Contains("project spans 259.2 s", error.Message);
        Assert.Contains("song.failed-attempt1.wav", error.Message);
        Assert.Contains("song.failed-attempt2.wav", error.Message);
        Assert.Contains("plugin X faulted", error.Message);
        var snapshot = fixture.Processes.Started[1].Info.ArgumentList[3];
        Assert.Contains(snapshot, error.Message);
        Assert.True(File.Exists(snapshot));
        Assert.False(File.Exists(fixture.Files.PathFor("song.wav")));
        Assert.True(File.Exists(fixture.Files.PathFor("song.failed-attempt1.wav")));
        Assert.True(File.Exists(fixture.Files.PathFor("song.failed-attempt2.wav")));
        Assert.Equal(3, fixture.Processes.Started.Count);
    }

    [Fact]
    public async Task ShortRenderWithACleanExitIsRetriedAndAcceptedWhenTheLengthRepeats()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        fixture.Bridge.Song = new SongExtent(41472, 41472, 0, 100, 96, 259.2);
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("song.wav"), 200);

        var result = Messages.Element(await session.RenderAsync("song.wav", 5, CancellationToken.None));

        Assert.Equal(3, fixture.Processes.Started.Count);
        var attempts = result.GetProperty("attempts").EnumerateArray().ToArray();
        Assert.Equal("failed", attempts[0].GetProperty("outcome").GetString());
        Assert.Contains("project spans 259.2 s", attempts[0].GetProperty("failure").GetString());
        Assert.Equal("accepted-short", attempts[1].GetProperty("outcome").GetString());
        Assert.Equal(200, result.GetProperty("seconds").GetDouble(), 3);
        var codes = result.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetProperty("code").GetString()).ToArray();
        Assert.Equal(new[] { "RenderRetried", "RenderShorterThanExpected" }, codes);
        Assert.True(File.Exists(fixture.Files.PathFor("song.failed-attempt1.wav")));
    }

    [Fact]
    public async Task RenderWithoutASpanEstimateSucceedsOnTheFirstCleanExit()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        fixture.Processes.OnRenderExit = _ => TestFiles.WriteWave(fixture.Files.PathFor("song.wav"), 3);

        var result = Messages.Element(await session.RenderAsync("song.wav", 5, CancellationToken.None));

        Assert.Equal(2, fixture.Processes.Started.Count);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("expectedSeconds").ValueKind);
        var attempt = Assert.Single(result.GetProperty("attempts").EnumerateArray());
        Assert.Equal("ok", attempt.GetProperty("outcome").GetString());
        Assert.Equal(3, result.GetProperty("seconds").GetDouble(), 3);
    }

    [Fact]
    public async Task TimedOutRenderIsNotRetriedAndItsPartialIsMovedAside()
    {
        using var fixture = new SessionFixture();
        fixture.Processes.HangRender = true;
        await using var session = fixture.Session();
        await session.LaunchAsync("song.flp", 1, CancellationToken.None);
        var onStart = fixture.Processes.OnStart;
        fixture.Processes.OnStart = info =>
        {
            onStart?.Invoke(info);
            if (info.ArgumentList[0] == "/R") TestFiles.WriteWave(fixture.Files.PathFor("song.wav"), 1); // the renderer's partial output
        };

        var error = await Assert.ThrowsAsync<IOException>(() => session.RenderAsync("song.wav", 1, CancellationToken.None));

        Assert.Equal(2, fixture.Processes.Started.Count);
        Assert.Contains("song.failed-attempt1.wav", error.Message);
        Assert.False(File.Exists(fixture.Files.PathFor("song.wav")));
    }

    [Fact]
    public async Task ExitedUnconfirmedSessionDoesNotPreventFailedNewLaunchCleanup()
    {
        using var fixture = new SessionFixture();
        await using var session = fixture.Session();
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        fixture.Bridge.OnPython = (_, _) => throw new BridgeCompletionUnknownException(new EndOfStreamException());
        await Assert.ThrowsAsync<BridgeCompletionUnknownException>(() => session.ExecutePythonAsync("result = 1", 10, CancellationToken.None));
        fixture.Processes.Started[0].Process.Terminate();
        fixture.Bridge.Available = false;
        await Assert.ThrowsAsync<TimeoutException>(() => session.LaunchAsync("new-generation.flp", 1, CancellationToken.None));
        Assert.True(fixture.Processes.Started[1].Process.Terminated);
    }
}
