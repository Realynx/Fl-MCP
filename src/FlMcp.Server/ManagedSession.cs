using System.Security.Cryptography;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>Owns one disposable project process; all editing and render transitions are serialized.</summary>
public sealed class ManagedSession(ServerSettings settings, IProcessHost processes, IBridgeClient bridge) : IAsyncDisposable
{
    private readonly WorkspacePaths paths = new(settings.Workspace);
    private readonly SemaphoreSlim gate = new(1, 1);
    private IManagedProcess? authoring;
    private string? token;
    private string? expectedProject;

    public async Task<SessionStatus> LaunchAsync(string projectPath, int timeoutSeconds, CancellationToken ct, string? sourceProjectPath = null)
    {
        ValidateTimeout(timeoutSeconds, 300);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        var launched = false;
        try
        {
            settings.ValidateLaunch();
            if (authoring is { HasExited: false }) throw new InvalidOperationException("A managed project is already open. Save and render it before starting another.");
            if (processes.HasRunningStudio()) throw new InvalidOperationException("Close other FL Studio processes first. FL MCP will not reuse a personal session.");
            var path = paths.NewFile(projectPath, ".flp");
            var source = sourceProjectPath is null ? settings.Template! : paths.Resolve(sourceProjectPath, ".flp");
            Artifacts.VerifyProject(source);
            File.Copy(source, path, overwrite: false);
            expectedProject = path;
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            authoring?.Dispose();
            authoring = null;
            authoring = processes.Start(LaunchCommands.Authoring(settings.Executable!, path, paths.Root, token));
            launched = true;
            return await AwaitReadyAsync(timeoutSeconds, ct).ConfigureAwait(false);
        }
        catch
        {
            if (launched) StopAuthoring();
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task<JsonElement> CallAsync(string operation, object args, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
            return await CallCoreAsync(operation, args, 30, ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<object> SaveAsync(string projectPath, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = await SaveCoreAsync(projectPath, ct).ConfigureAwait(false);
            return new { path, bytes = Artifacts.VerifyProject(path) };
        }
        finally { gate.Release(); }
    }

    public async Task<object> RenderAsync(string outputPath, int timeoutSeconds, CancellationToken ct)
    {
        ValidateTimeout(timeoutSeconds, 3600);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var output = paths.NewFile(outputPath, ".wav");
            var snapshotName = Path.Combine("snapshots", Guid.NewGuid().ToString("N"), Path.GetFileNameWithoutExtension(output) + ".flp");
            var snapshot = await SaveCoreAsync(snapshotName, ct).ConfigureAwait(false);
            // The owned editor is disposable; snapshot validity is checked before its process is stopped.
            StopAuthoring();
            if (processes.HasRunningStudio()) throw new InvalidOperationException($"Another FL process is running. Snapshot preserved at {snapshot}; close it before retrying.");
            using var render = processes.Start(LaunchCommands.Render(settings.Executable!, snapshot, Path.GetDirectoryName(output)!));
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await render.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                if (render.ExitCode != 0) throw new IOException($"FL render exited with code {render.ExitCode}. Snapshot: {snapshot}");
                return new { path = output, bytes = Artifacts.VerifyWave(output), project = snapshot, sessionClosed = true };
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                throw new IOException($"Render failed. Snapshot preserved at {snapshot}. {ex.Message}", ex);
            }
            finally { render.Terminate(); }
        }
        finally { gate.Release(); }
    }

    public async Task<object> CloseAsync(string projectPath, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = await SaveCoreAsync(projectPath, ct).ConfigureAwait(false);
            StopAuthoring();
            return new { project = path, sessionClosed = true };
        }
        finally { gate.Release(); }
    }

    private async Task<SessionStatus> AwaitReadyAsync(int timeoutSeconds, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            while (true)
            {
                if (authoring!.HasExited) throw new IOException("FL Studio exited before the plugin was ready. Check installation and enabled plugin state.");
                var status = await TryStatusAsync(deadline.Token).ConfigureAwait(false);
                if (status is { Available: true } && HasExpectedProject(status)) return status;
                await Task.Delay(250, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("FL MCP was not ready before the deadline. Install FruityLink and enable FL MCP once in its Plugins menu; check the host log for build support.");
        }
    }

    private async Task<SessionStatus?> TryStatusAsync(CancellationToken ct)
    {
        try
        {
            var result = await CallCoreAsync("status", new { }, 1, ct).ConfigureAwait(false);
            var status = result.Deserialize<SessionStatus>(Messages.Json);
            if (status?.ProcessId != authoring!.Id) throw new InvalidDataException("Bridge process identity does not match the launched FL instance.");
            return status;
        }
        catch (IOException) { return null; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
    }

    private async Task<string> SaveCoreAsync(string projectPath, CancellationToken ct)
    {
        await RequireProjectIdentityAsync(ct).ConfigureAwait(false);
        var path = paths.NewFile(projectPath, ".flp");
        await CallCoreAsync("save", new PathArgs(path), 60, ct).ConfigureAwait(false);
        Artifacts.VerifyProject(path);
        return path;
    }

    private async Task RequireProjectIdentityAsync(CancellationToken ct)
    {
        var status = await TryStatusAsync(ct).ConfigureAwait(false);
        if (status is not { Available: true } || !HasExpectedProject(status))
            throw new InvalidOperationException("FL no longer reports the expected managed project. No edit or save was attempted.");
    }

    private bool HasExpectedProject(SessionStatus status)
    {
        var line = status.Project.Split('\n').LastOrDefault(value => value.StartsWith("Path: ", StringComparison.Ordinal));
        if (line is null) return false;
        var reportedPath = line[6..].Trim();
        return Path.IsPathFullyQualified(reportedPath) &&
            string.Equals(Path.GetFullPath(reportedPath), expectedProject, StringComparison.OrdinalIgnoreCase);
    }

    private Task<JsonElement> CallCoreAsync(string operation, object args, int timeoutSeconds, CancellationToken ct)
    {
        if (authoring is null || authoring.HasExited || token is null)
            throw new InvalidOperationException("No managed FL project is ready. Call fl_project_start first.");
        return bridge.CallAsync(authoring.Id, token, operation, args, timeoutSeconds, ct);
    }

    private void StopAuthoring()
    {
        authoring?.Terminate();
        authoring?.Dispose();
        authoring = null;
        token = null;
        expectedProject = null;
    }

    private static void ValidateTimeout(int seconds, int maximum)
    {
        if (seconds < 1 || seconds > maximum) throw new ArgumentOutOfRangeException(nameof(seconds), $"Timeout must be 1..{maximum} seconds.");
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { StopAuthoring(); }
        finally { gate.Release(); }
    }
}
