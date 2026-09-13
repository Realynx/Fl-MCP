using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlMcp.Protocol;

namespace FlMcp.Server;

/// <summary>Recovery policy for a disposable process only. Never used by observed/attached sessions.</summary>
public sealed partial class OwnedDialogMonitor
{
    private readonly WorkspacePaths paths;
    private readonly string phase;
    private readonly string project;
    private readonly TimeProvider clock;
    private readonly Dictionary<nint, DateTimeOffset> unverifiedForms = [];
    private readonly List<SessionWarning> warnings = [];
    private StudioDialog? answeredDialog;
    private DateTimeOffset answeredAt;
    private readonly HashSet<string> answeredPrompts = [];

    public string OriginalProject { get; }
    public string OriginalSha256 { get; }
    public IReadOnlyList<SessionWarning> Warnings => warnings.ToArray();

    public OwnedDialogMonitor(WorkspacePaths paths, string project, string phase, TimeProvider? clock = null)
    {
        this.paths = paths;
        this.project = paths.Resolve(project, ".flp");
        this.phase = phase;
        this.clock = clock ?? TimeProvider.System;
        // Preserve bytes before FL has an opportunity to repair or save its input. The source
        // template/snapshot remains separate too; recovery never overwrites a user's source.
        OriginalProject = paths.NewFile(Path.Combine("recovery", Guid.NewGuid().ToString("N"), "original.flp"), ".flp");
        using var input = new FileStream(this.project, FileMode.Open, FileAccess.Read, FileShare.Read);
        using (var output = new FileStream(OriginalProject, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            input.CopyTo(output);
        input.Position = 0;
        OriginalSha256 = Convert.ToHexString(SHA256.HashData(input));
    }

    /// <returns>True while a recognized, answered modal has not yet closed.</returns>
    public bool Inspect(IManagedProcess process, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dialogs = process.ReadDialogs(ct);
        if (dialogs.Count == 0) return false;
        var dialog = dialogs[0];
        if (dialog.ProcessId != process.Id)
            throw new IOException("Dialog process identity did not match the owned FL process. No response was sent.");
        if (Initializing(dialog)) return true;
        if (answeredDialog is not null && SameDialog(dialog, answeredDialog))
        {
            if (clock.GetUtcNow() - answeredAt < TimeSpan.FromSeconds(5)) return true;
            throw Failure(dialog, "The recognized dialog did not close after its response; no second response was sent.");
        }
        var button = InvalidNotesDialog.ResponseButton(dialog, phase);
        var prompt = InvalidNotesDialog.Prompt(dialog);
        if (button is null || answeredPrompts.Contains(prompt))
            throw Failure(dialog, "FL is waiting for a dialog that this operation cannot safely answer. No response was sent.");
        RequireUnchangedInput(dialog);
        var skipSave = prompt == InvalidNotesDialog.SavePrompt;
        var diagnostic = WriteDiagnostic(dialog, skipSave ? "save-abort-requested" : "recovery-requested", button.Caption);
        if (!process.TryRespondToDialog(dialog, button, ct))
            throw Failure(dialog, "The dialog changed or its verified response could not be sent. No further response was attempted.");
        answeredPrompts.Add(prompt);
        answeredDialog = dialog;
        answeredAt = clock.GetUtcNow();
        warnings.Add(skipSave
            ? new("invalid-notes-save-skipped", "Declined a startup save containing invalid notes. Its destination was unconfirmed; no destructive save recovery was accepted.", OriginalProject, diagnostic)
            : new("invalid-notes-recovery", "Accepted FL's exact request to delete invalid notes while loading a disposable copy. Compare the resulting notes with the preserved original before continuing.", OriginalProject, diagnostic));
        return true;
    }

    private bool Initializing(StudioDialog dialog)
    {
        if (dialog.WindowClass != "TMsgForm" || dialog.VerifiedNativeChoices) return false;
        if (!unverifiedForms.TryGetValue(dialog.Window, out var first))
            unverifiedForms[dialog.Window] = first = clock.GetUtcNow();
        return clock.GetUtcNow() - first < TimeSpan.FromSeconds(1);
    }

    private void RequireUnchangedInput(StudioDialog dialog)
    {
        using var current = new FileStream(project, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Convert.ToHexString(SHA256.HashData(current)) != OriginalSha256)
            throw Failure(dialog, "The disposable project changed since launch. Automatic recovery was refused.");
    }

    private static bool SameDialog(StudioDialog left, StudioDialog right) =>
        left.Window == right.Window && left.Title == right.Title && left.WindowClass == right.WindowClass &&
        left.Text.SequenceEqual(right.Text) && left.Buttons.SequenceEqual(right.Buttons);

    private IOException Failure(StudioDialog dialog, string reason)
    {
        var diagnostic = WriteDiagnostic(dialog, "blocked", null);
        var description = Clean(string.Join(" ", dialog.Text), 1200);
        return new IOException($"{reason} {phase} dialog: '{Clean(dialog.Title, 200)}' ({Clean(dialog.WindowClass, 100)}). {description} " +
            $"Original project preserved at {OriginalProject}. Diagnostic: {diagnostic}. Inspect a separate copy in FL and retry with a new output path.");
    }

    private string WriteDiagnostic(StudioDialog dialog, string action, string? response)
    {
        var path = paths.NewFile(Path.Combine("recovery", Guid.NewGuid().ToString("N"), "dialog.json"), ".json");
        var value = new
        {
            phase, action, response, dialog.VerifiedNativeChoices, processId = dialog.ProcessId, title = Clean(dialog.Title, 200),
            windowClass = Clean(dialog.WindowClass, 100), text = dialog.Text.Take(64).Select(value => Clean(value, 2048)).ToArray(),
            childClasses = dialog.ChildClasses.Take(64).Select(value => Clean(value, 128)).ToArray(),
            buttons = dialog.Buttons.Take(16).Select(button => new { button.Id, caption = Clean(button.Caption, 100) }).ToArray(),
            project, originalProject = OriginalProject, originalSha256 = OriginalSha256, recordedAt = DateTimeOffset.UtcNow
        };
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(output, value, Messages.Json);
        return path;
    }

    private static string Clean(string value, int maximum)
    {
        var safe = PrivateToken().Replace(new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()), "[redacted]");
        return safe.Length <= maximum ? safe : safe[..maximum];
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{64}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateToken();
}
