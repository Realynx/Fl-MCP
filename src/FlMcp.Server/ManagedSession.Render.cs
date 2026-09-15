using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>One renderer run. Outcome is "ok", "failed", or "accepted-short" (a WAV shorter than the project's span
/// whose length the retry reproduced exactly, so the renderer is consistent rather than crashed).</summary>
public sealed record RenderAttempt(int Attempt, string Outcome, int? ExitCode, double ElapsedSeconds, double? Seconds, long? Bytes,
    double? ExpectedSeconds, string? PartialPath, string? Failure);

/// <summary>What fl_project_render returns: the WAV, the snapshot it was rendered from, the untrimmed project of a
/// section render, the isolation record, every renderer attempt and the warnings. A render always closes the session.</summary>
public sealed record RenderResult(string Path, long? Bytes, double? Seconds, double? ExpectedSeconds, string Project, string? FullProject,
    JsonElement? Range, RenderAttempt[] Attempts, bool SessionClosed, SessionWarning[] Warnings);

public sealed partial class ManagedSession
{
    private const int RenderAttempts = 2;
    /// <summary>A WAV shorter than this fraction of the project's span counts as a partial render.</summary>
    private const double ShortRenderFraction = 0.9;

    public async Task<RenderResult> RenderAsync(string outputPath, int timeoutSeconds, CancellationToken ct, RenderRange? range = null)
    {
        ValidateTimeout(timeoutSeconds, 3600);
        range?.Validate();
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await RenderCoreAsync(outputPath, timeoutSeconds, ct, range).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    /// <summary>The render itself; the caller holds the gate and has validated the deadline and range.</summary>
    private async Task<RenderResult> RenderCoreAsync(string outputPath, int timeoutSeconds, CancellationToken ct, RenderRange? range)
    {
        {
            RequireOwnedLifecycle("render");
            var output = paths.NewFile(outputPath, ".wav");
            var snapshotDirectory = Path.Combine("snapshots", Guid.NewGuid().ToString("N"));
            var stem = Path.GetFileNameWithoutExtension(output);
            string? fullProject = null;
            JsonElement? isolation = null;
            if (range is not null)
            {
                // Preserve the untrimmed project first: isolating the range edits the live disposable project.
                fullProject = await SaveCoreAsync(Path.Combine(snapshotDirectory, stem + "-full.flp"), ct).ConfigureAwait(false);
                isolation = await IsolateRangeAsync(range, fullProject, ct).ConfigureAwait(false);
            }
            var snapshot = await SaveCoreAsync(Path.Combine(snapshotDirectory, stem + ".flp"), ct).ConfigureAwait(false);
            // Read the span FL will export while the editor is still alive; a finished WAV is checked against it.
            var extent = await TryReadSongExtentAsync(ct).ConfigureAwait(false);
            var renderInBackground = ownedBackground;
            // The owned editor is disposable; snapshot validity is checked before its process is stopped.
            StopAuthoring();
            if (!renderInBackground && processes.HasRunningStudio()) throw new InvalidOperationException($"Another FL process is running. Snapshot preserved at {snapshot}; close it before retrying.");
            var attempts = new List<RenderAttempt>();
            var warnings = new List<SessionWarning>(launchWarnings);
            for (var attempt = 1; attempt <= RenderAttempts; attempt++)
            {
                var dialogs = new OwnedDialogMonitor(paths, snapshot, "Render");
                var result = await RenderOnceAsync(attempt, snapshot, output, timeoutSeconds, renderInBackground, extent,
                    attempts.LastOrDefault(), dialogs, ct).ConfigureAwait(false);
                warnings.AddRange(dialogs.Warnings);
                attempts.Add(result);
                if (result.Outcome == "failed") continue;
                if (attempt > 1)
                    warnings.Add(new SessionWarning("RenderRetried",
                        $"Render attempt {attempt - 1} failed ({attempts[^2].Failure}) and was retried from the same snapshot; the partial output was moved to {attempts[^2].PartialPath ?? "(no file)"}.",
                        dialogs.OriginalProject, DescribeAttempts(attempts)));
                if (result.Outcome == "accepted-short")
                    warnings.Add(new SessionWarning("RenderShorterThanExpected",
                        $"Both attempts produced the same length: {result.Failure}. FL's exporter is consistent, so the span estimate (constant tempo) is what differs; check tempo automation and the last marker.",
                        dialogs.OriginalProject, DescribeAttempts(attempts)));
                return new RenderResult(output, result.Bytes, result.Seconds, extent?.Seconds, snapshot, fullProject, isolation,
                    attempts.ToArray(), SessionClosed: true, warnings.ToArray());
            }
            throw new IOException($"Render failed {attempts.Count} times from snapshot {snapshot}. {DescribeAttempts(attempts)} " +
                "Resume the snapshot with fl_project_start(sourceProjectPath) and inspect the plugins named in the host log before rendering again." + HostLogTail());
        }
    }

    private async Task<RenderAttempt> RenderOnceAsync(int attempt, string snapshot, string output, int timeoutSeconds, bool background,
        SongExtent? extent, RenderAttempt? previous, OwnedDialogMonitor dialogs, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var render = processes.Start(LaunchCommands.Render(settings.Executable!, snapshot, Path.GetDirectoryName(output)!),
            background, deadline.Token);
        try
        {
            await AwaitRenderAsync(render, dialogs, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A spent deadline or an unanswerable dialog is not retried: the budget is gone or a person is needed.
            var moved = MovePartialAside(output, attempt);
            throw new IOException($"Render failed. Snapshot preserved at {snapshot}. {ex.Message}" +
                (moved is null ? "" : $" The partial output was moved to {moved}."), ex);
        }
        finally { render.Terminate(); }
        return ClassifyAttempt(attempt, render.ExitCode, Math.Round(Stopwatch.GetElapsedTime(started).TotalSeconds, 1), output, extent, previous);
    }

    /// <summary>Nonzero exit, an invalid WAV, or a WAV shorter than the project's span is a failed attempt whose partial
    /// output is moved aside. A short WAV whose length the previous attempt already produced is accepted: the renderer
    /// is deterministic, so the constant-tempo span estimate is what is off.</summary>
    private static RenderAttempt ClassifyAttempt(int attempt, int exitCode, double elapsed, string output, SongExtent? extent, RenderAttempt? previous)
    {
        var (wave, readFailure) = TryReadWave(output);
        var expected = extent is { Seconds: > 0 } ? extent.Seconds : (double?)null;
        var failure = exitCode != 0 ? $"FL render exited with code {exitCode}" + (readFailure is null ? "" : $" ({readFailure})") : readFailure;
        if (failure is null && ShortBy(wave!, expected) is string shortBy)
        {
            failure = $"{shortBy} at {extent!.Tempo} BPM";
            if (previous is { Seconds: double prior } && Math.Abs(prior - wave!.Seconds) <= 0.01 * expected!.Value)
                return new RenderAttempt(attempt, "accepted-short", exitCode, elapsed, wave.Seconds, wave.Bytes, expected, null, failure);
        }
        if (failure is null) return new RenderAttempt(attempt, "ok", exitCode, elapsed, wave!.Seconds, wave.Bytes, expected, null, null);
        var partialBytes = File.Exists(output) ? new FileInfo(output).Length : (long?)null;
        return new RenderAttempt(attempt, "failed", exitCode, elapsed, wave?.Seconds, partialBytes, expected, MovePartialAside(output, attempt), failure);
    }

    private static (WaveInfo? Wave, string? Failure) TryReadWave(string output)
    {
        try { return (Artifacts.ReadWave(output), null); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return (null, ex.Message); }
    }

    private static string? ShortBy(WaveInfo wave, double? expected) =>
        expected is double span && wave.Seconds < ShortRenderFraction * span ? $"the WAV is {wave.Seconds:F1} s but the project spans {span:F1} s" : null;

    /// <summary>Renames a partial output beside itself so the path is free for a retry and the evidence survives.</summary>
    private static string? MovePartialAside(string output, int attempt)
    {
        if (!File.Exists(output)) return null;
        var directory = Path.GetDirectoryName(output)!;
        var stem = Path.GetFileNameWithoutExtension(output);
        var target = Path.Combine(directory, $"{stem}.failed-attempt{attempt}.wav");
        if (File.Exists(target)) target = Path.Combine(directory, $"{stem}.failed-attempt{attempt}-{Guid.NewGuid():N}.wav");
        try { File.Move(output, target); }
        catch (IOException) { return output; } // still locked: leave it where it is and say so
        return target;
    }

    private async Task<SongExtent?> TryReadSongExtentAsync(CancellationToken ct)
    {
        try
        {
            var extent = (await CallCoreAsync("song", new { }, 30, ct).ConfigureAwait(false)).Deserialize<SongExtent>(Messages.Json);
            return extent is { EndTick: > 0, Seconds: > 0 } ? extent : null;
        }
        catch (InvalidOperationException) { return null; } // a plugin build without the operation
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    private static string DescribeAttempts(IEnumerable<RenderAttempt> attempts) => string.Join(" ", attempts.Select(a =>
        $"Attempt {a.Attempt}: {(a.Outcome == "ok" ? "ok" : a.Failure)}" + (a.ExitCode is int code ? $", exit code {code}" : "") +
        $", {a.ElapsedSeconds:F1} s" + (a.Seconds is double s ? $", WAV {s:F1} s" : "") + (a.Bytes is long b ? $" ({b} bytes)" : "") +
        (a.ExpectedSeconds is double e ? $", project spans {e:F1} s" : "") +
        (a.PartialPath is null ? "" : $", partial output moved to {a.PartialPath}") + "."));

    /// <summary>The last lines of today's FruityLink plugin-host log, where a plugin that took the renderer down is named.</summary>
    private string HostLogTail()
    {
        try
        {
            var file = Path.Combine(settings.HostLogDirectory, $"plugin-host-{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(file)) return "";
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = (int)Math.Min(stream.Length, 16 * 1024);
            stream.Seek(-length, SeekOrigin.End);
            var buffer = new byte[length];
            stream.ReadExactly(buffer);
            var lines = Encoding.UTF8.GetString(buffer).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();
            return $" Host log tail ({file}): " + string.Join(" | ", lines.Skip(Math.Max(0, lines.Length - 8)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ""; }
    }
}
