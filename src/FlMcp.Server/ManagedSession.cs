using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>Owns a disposable process or observes an explicitly attached process; serializes session operations.</summary>
public sealed partial class ManagedSession(ServerSettings settings, IProcessHost processes, IBridgeClient bridge, IInstanceSource? discovery = null) : IAsyncDisposable
{
    private readonly WorkspacePaths paths = new(settings.Workspace);
    private readonly SemaphoreSlim gate = new(1, 1);
    private IManagedProcess? authoring;
    private string? token;
    private string? expectedProject;
    private bool embeddedCompletionUnknown;
    private IReadOnlyList<SessionWarning> launchWarnings = [];
    private bool ownedBackground;

    /// <summary>File name of the fruitylink wheel this server hands to FL, so documentation can name the installed contract.</summary>
    public string InstalledPythonPackage =>
        settings.PythonPackagePath is { } explicitPackage ? Path.GetFileName(explicitPackage)
        : settings.Executable is not null ? Path.GetFileName(settings.ResolvePythonPackage())
        : "unknown (FL executable not configured)";

    public async Task<SessionStatus> LaunchAsync(string projectPath, int timeoutSeconds, CancellationToken ct,
        string? sourceProjectPath = null, bool background = false)
    {
        ValidateTimeout(timeoutSeconds, 300);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await LaunchCoreAsync(projectPath, timeoutSeconds, ct, sourceProjectPath, background).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    /// <summary>The launch itself; the caller holds the gate (fl_section_measure reopens a rendered snapshot under one gate).</summary>
    private async Task<SessionStatus> LaunchCoreAsync(string projectPath, int timeoutSeconds, CancellationToken ct,
        string? sourceProjectPath, bool background)
    {
        var launched = false;
        try
        {
            settings.ValidateLaunch();
            if (authoring is { HasExited: false }) throw new InvalidOperationException("An FL session is already connected. Detach it or save/close a disposable session before starting another.");
            if (!background && processes.HasRunningStudio()) throw new InvalidOperationException("Close other FL Studio processes first. FL MCP will not reuse a personal session.");
            var path = paths.NewFile(projectPath, ".flp");
            var source = sourceProjectPath is null ? settings.Template! : paths.Resolve(sourceProjectPath, ".flp");
            Artifacts.VerifyProjectSource(source);
            File.Copy(source, path, overwrite: false);
            var dialogs = new OwnedDialogMonitor(paths, path, "Launch");
            expectedProject = path;
            attachedProject = null;
            attachmentPaths = null;
            leaseToken = null;
            ownership = SessionOwnership.Owned;
            ownedBackground = background;
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            authoring?.Dispose();
            authoring = null;
            embeddedCompletionUnknown = false;
            launchWarnings = [];
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                authoring = processes.Start(LaunchCommands.Authoring(settings.Executable!, path, paths.Root, token,
                    settings.ResolvePythonRuntime(), settings.ResolvePythonPackage()), background, deadline.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw ReadinessTimeout(dialogs);
            }
            launched = true;
            var status = await AwaitReadyAsync(dialogs, deadline.Token, ct).ConfigureAwait(false);
            status = await AwaitSettledAsync(status, deadline.Token, ct).ConfigureAwait(false);
            var warnings = dialogs.Warnings.ToList();
            if (status.Settle is { Stable: false } settle)
                warnings.Add(new SessionWarning("ProjectUnsettled",
                    $"Tempo, PPQ or title were still changing {settle.Milliseconds} ms after readiness; the values here are the last observed. Read fl_status again before relying on tempo {status.Tempo}.",
                    dialogs.OriginalProject, $"first tempo {settle.FirstTempo}, polls {settle.Polls}"));
            launchWarnings = warnings;
            return status with { Warnings = launchWarnings };
        }
        catch
        {
            if (launched) StopAuthoring();
            throw;
        }
    }

    public async Task<JsonElement> CallAsync(string operation, object args, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (ownership == SessionOwnership.Attached && operation == "status")
                return await CallCoreAsync("status", args, 5, ct).ConfigureAwait(false);
            await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            var result = await CallCoreAsync(operation, args, 30, ct).ConfigureAwait(false);
            return operation == "status" && launchWarnings.Count > 0
                ? Messages.Element(result.Deserialize<SessionStatus>(Messages.Json)! with { Warnings = launchWarnings })
                : result;
        }
        finally { gate.Release(); }
    }

    public async Task<object> SaveAsync(string projectPath, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = await SaveCoreAsync(projectPath, ct).ConfigureAwait(false);
            return new { path, bytes = Artifacts.VerifyProject(path), warnings = launchWarnings };
        }
        finally { gate.Release(); }
    }

    public async Task<object> CloseAsync(string projectPath, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            RequireOwnedLifecycle("close");
            var path = await SaveCoreAsync(projectPath, ct).ConfigureAwait(false);
            StopAuthoring();
            return new { project = path, sessionClosed = true, warnings = launchWarnings };
        }
        finally { gate.Release(); }
    }

    private async Task<SessionStatus> AwaitReadyAsync(OwnedDialogMonitor dialogs, CancellationToken deadline,
        CancellationToken callerCancellation)
    {
        try
        {
            while (true)
            {
                if (authoring!.HasExited) throw new IOException($"FL Studio exited before the plugin was ready. Check installation and enabled plugin state. Original project: {dialogs.OriginalProject}");
                if (!dialogs.Inspect(authoring, deadline))
                {
                    var status = await TryStatusAsync(deadline).ConfigureAwait(false);
                    if (status is { Available: true } && HasExpectedProject(status))
                    {
                        authoring.CompleteStartup();
                        return status;
                    }
                }
                await Task.Delay(250, deadline).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
        {
            throw ReadinessTimeout(dialogs);
        }
    }

    /// <summary>FL answers status with the copied project's path before it has applied that project's tempo (live
    /// 2026-09-14: a 100 BPM snapshot reported the template's 140 for a few seconds). Keep polling until tempo, PPQ
    /// and title stay unchanged for the settle window; give up after the settle timeout or the launch deadline.</summary>
    private async Task<SessionStatus> AwaitSettledAsync(SessionStatus first, CancellationToken deadline, CancellationToken callerCancellation)
    {
        var started = Stopwatch.GetTimestamp();
        var stableSince = started;
        var last = first;
        var polls = 0;
        ProjectSettle Settle(bool stable) => new(stable, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds, polls, first.Tempo);
        try
        {
            while (true)
            {
                if (Stopwatch.GetElapsedTime(stableSince) >= settings.ProjectSettleWindow) return last with { Settle = Settle(true) };
                if (Stopwatch.GetElapsedTime(started) >= settings.ProjectSettleTimeout) return last with { Settle = Settle(false) };
                await Task.Delay(settings.ProjectSettlePollInterval, deadline).ConfigureAwait(false);
                var status = await TryStatusAsync(deadline).ConfigureAwait(false);
                polls++;
                if (status is not { Available: true } || !HasExpectedProject(status)) continue;
                if (SameProjectValues(status, last)) continue;
                last = status;
                stableSince = Stopwatch.GetTimestamp();
            }
        }
        catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
        {
            return last with { Settle = Settle(false) }; // the launch deadline bounds the wait; FL is ready regardless
        }
    }

    private static bool SameProjectValues(SessionStatus a, SessionStatus b) =>
        a.Tempo.Equals(b.Tempo) && a.Ppq == b.Ppq && a.ProjectTitle == b.ProjectTitle && a.ProjectPath == b.ProjectPath;

    private static TimeoutException ReadinessTimeout(OwnedDialogMonitor dialogs) => new(
        $"FL MCP was not ready before the deadline. Install FruityLink and enable FL MCP once in its Plugins menu; check the host log for build support. Original project: {dialogs.OriginalProject}");

    private static async Task AwaitRenderAsync(IManagedProcess render, OwnedDialogMonitor dialogs, CancellationToken ct)
    {
        using var monitoring = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var completion = render.WaitForExitAsync(monitoring.Token);
        try
        {
            while (!completion.IsCompleted)
            {
                dialogs.Inspect(render, ct);
                await Task.WhenAny(completion, Task.Delay(250, ct)).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            }
            await completion.ConfigureAwait(false);
        }
        finally
        {
            monitoring.Cancel();
            try { await completion.ConfigureAwait(false); }
            catch (OperationCanceledException) when (monitoring.IsCancellationRequested) { }
        }
    }

    private async Task<SessionStatus?> TryStatusAsync(CancellationToken ct)
    {
        try
        {
            var result = await CallCoreAsync("status", new { }, ownership == SessionOwnership.Attached ? 5 : 1, ct).ConfigureAwait(false);
            var status = result.Deserialize<SessionStatus>(Messages.Json);
            if (status?.ProcessId != authoring!.Id) throw new InvalidDataException("Bridge process identity does not match the launched FL instance.");
            // The plugin serves one operation at a time: a status reply acknowledges any earlier
            // embedded invocation has drained, even if that invocation's pipe response was lost.
            embeddedCompletionUnknown = false;
            return status;
        }
        catch (IOException) { return null; }
        // A status poll that does not answer inside its own one/five second budget means "not ready yet", never a
        // failure: the readiness and settle loops poll through it. BridgeClient now reports that budget as a
        // TimeoutException so ordinary tool calls get a readable error instead of a bare cancellation, so both
        // shapes have to land here.
        catch (TimeoutException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    private async Task<string> SaveCoreAsync(string projectPath, CancellationToken ct)
    {
        await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
        var path = (attachmentPaths ?? paths).NewFile(projectPath, ".flp");
        await CallCoreAsync(ownership == SessionOwnership.Attached ? "snapshot" : "save", new PathArgs(path), 60, ct).ConfigureAwait(false);
        Artifacts.VerifyProject(path);
        return path;
    }

    /// <summary>Confirms the connected FL still serves the expected project and returns its status (tempo, PPQ).</summary>
    private async Task<SessionStatus> RequireProjectIdentityAsync(CancellationToken ct)
    {
        var status = await TryStatusAsync(ct).ConfigureAwait(false);
        if (status is not { Available: true } || !HasExpectedProject(status))
            throw new InvalidOperationException(ownership == SessionOwnership.Attached
                ? "The attached project changed or FL is unavailable. Call fl_attach again before editing. No edit or save was attempted."
                : "FL no longer reports the expected managed project. No edit or save was attempted.");
        return status;
    }

    private bool HasExpectedProject(SessionStatus status)
    {
        if (ownership == SessionOwnership.Attached)
            return attachedProject?.Matches(ProjectIdentity.FromStatus(status)) == true;
        var line = status.Project.Split('\n').LastOrDefault(value => value.StartsWith("Path: ", StringComparison.Ordinal));
        var reportedPath = status.ProjectPath ?? line?[6..].Trim();
        if (reportedPath is null) return false;
        return Path.IsPathFullyQualified(reportedPath) &&
            string.Equals(Path.GetFullPath(reportedPath), expectedProject, StringComparison.OrdinalIgnoreCase);
    }

    private Task<JsonElement> CallCoreAsync(string operation, object args, int timeoutSeconds, CancellationToken ct)
    {
        if (authoring is null || authoring.HasExited || token is null)
            throw new InvalidOperationException("No FL project is connected. Call fl_project_start or fl_instances then fl_attach first.");
        return ownership == SessionOwnership.Attached
            ? bridge.CallAttachedAsync(authoring.Id, token, leaseToken!, operation, args, timeoutSeconds, ct)
            : bridge.CallAsync(authoring.Id, token, operation, args, timeoutSeconds, ct);
    }

    private void StopAuthoring()
    {
        RequireOwnedLifecycle("stop");
        if (embeddedCompletionUnknown && authoring is { HasExited: false })
            throw new InvalidOperationException("Embedded Python completion is unconfirmed. FL was left running; reconnect and obtain status before closing it.");
        authoring?.Terminate();
        authoring?.Dispose();
        authoring = null;
        token = null;
        expectedProject = null;
        ownership = SessionOwnership.None;
        ownedBackground = false;
    }

    private static void ValidateTimeout(int seconds, int maximum)
    {
        if (seconds < 1 || seconds > maximum) throw new ArgumentOutOfRangeException(nameof(seconds), $"Timeout must be 1..{maximum} seconds.");
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (ownership == SessionOwnership.Attached)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await DetachCoreAsync(timeout.Token).ConfigureAwait(false); }
                catch (Exception) { ResetAttachment(); } // Never terminate user FL, including a disconnected lease.
            }
            else StopAuthoring();
        }
        finally { gate.Release(); }
    }
}
