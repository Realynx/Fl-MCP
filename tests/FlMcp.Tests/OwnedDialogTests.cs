using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlMcp.Protocol;
using FlMcp.Server;
using Xunit;

namespace FlMcp.Tests;

public sealed class OwnedDialogTests
{
    [Fact]
    public void OriginalBytesArePreservedBeforeProcessCanRun()
    {
        using var files = new TestFiles();
        var project = files.Project();
        var expected = File.ReadAllBytes(project);
        var monitor = new OwnedDialogMonitor(new(files.Root), project, "Launch");
        File.WriteAllText(project, "changed by disposable FL");
        Assert.Equal(expected, File.ReadAllBytes(monitor.OriginalProject));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)), monitor.OriginalSha256);
        Assert.Empty(monitor.Warnings);
    }

    [Fact]
    public void UnknownDialogStopsWithBoundedRedactedDiagnosticWithoutClicking()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Render");
        var secret = new string('A', 64);
        var process = new DialogProcess { Dialog = Dialog(100, "Registration", "Missing license\r\n" + secret) };
        var error = Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Contains("Registration", error.Message);
        Assert.Contains(monitor.OriginalProject, error.Message);
        Assert.DoesNotContain(secret, error.Message);
        Assert.Equal(0, process.Responses);
        var log = Assert.Single(Directory.GetFiles(files.Root, "dialog.json", SearchOption.AllDirectories));
        var text = File.ReadAllText(log);
        Assert.DoesNotContain(secret, text);
        var value = JsonDocument.Parse(text).RootElement;
        Assert.Equal("blocked", value.GetProperty("action").GetString());
        Assert.Equal(monitor.OriginalSha256, value.GetProperty("originalSha256").GetString());
    }

    [Fact]
    public void RenderProgressIsIgnoredWithoutReceivingInput()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Render");
        var process = new DialogProcess { Dialogs = [RenderProgress(100)] };

        Assert.False(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(0, process.Responses);
        Assert.Empty(monitor.Warnings);
    }

    [Fact]
    public void RenderProgressClassIsNotIgnoredDuringAuthoring()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Launch");
        var process = new DialogProcess { Dialogs = [RenderProgress(100)] };

        Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(0, process.Responses);
    }

    [Fact]
    public void RenderProgressDoesNotHideAnotherActionableDialog()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Render");
        var process = new DialogProcess
        {
            Dialogs = [RenderProgress(100), Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt)]
        };

        Assert.True(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(1, process.Responses);
        Assert.Equal(6, process.LastResponse);
    }

    [Fact]
    public void RenderProgressDoesNotHideUnknownDialog()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Render");
        var process = new DialogProcess
        {
            Dialogs = [RenderProgress(100), Dialog(100, "Render error", "A plugin failed to load.")]
        };

        Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(0, process.Responses);
    }

    [Fact]
    public void SpoofedProcessCannotReceiveAResponse()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Launch");
        var process = new DialogProcess { Dialog = Dialog(999, "Invalid data", "There are invalid notes in this project.") };
        Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(0, process.Responses);
        Assert.Empty(Directory.GetFiles(files.Root, "dialog.json", SearchOption.AllDirectories));
    }

    [Fact]
    public void ExactLoadPromptRespondsOnceOnlyAfterPreservingOriginalBytes()
    {
        using var files = new TestFiles();
        var project = files.Project();
        var expected = File.ReadAllBytes(project);
        var monitor = new OwnedDialogMonitor(new(files.Root), project, "Launch");
        var process = new DialogProcess { Dialog = Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt) };
        process.BeforeResponse = () => Assert.Equal(expected, File.ReadAllBytes(monitor.OriginalProject));
        Assert.True(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(1, process.Responses);
        Assert.False(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal("invalid-notes-recovery", Assert.Single(monitor.Warnings).Code);
        process.Dialog = Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt) with { Window = 200 };
        Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(1, process.Responses);
    }

    [Theory]
    [InlineData("Other title", InvalidNotesDialog.LoadPrompt, "#32770")]
    [InlineData("Invalid data", "There are invalid notes in this project. These notes will be deleted when the project is saved.", "#32770")]
    [InlineData("Invalid data", InvalidNotesDialog.LoadPrompt, "UnverifiedDialogClass")]
    [InlineData("Invalid data", InvalidNotesDialog.LoadPrompt + " Also delete samples?", "#32770")]
    public void SimilarButUnverifiedPromptsNeverReceiveResponse(string title, string body, string windowClass)
    {
        var dialog = Dialog(100, title, body) with { WindowClass = windowClass };
        Assert.Null(InvalidNotesDialog.ResponseButton(dialog));
    }

    [Fact]
    public void ExactTextRequiresBothVerifiedButtonIdentifiersAndLabels()
    {
        var dialog = Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt);
        Assert.NotNull(InvalidNotesDialog.ResponseButton(dialog));
        Assert.Null(InvalidNotesDialog.ResponseButton(dialog with { Buttons = [new(1, 7, "Yes"), new(2, 6, "No")] }));
        Assert.Null(InvalidNotesDialog.ResponseButton(dialog with { Buttons = [new(1, 6, "Yes")] }));
        Assert.Null(InvalidNotesDialog.ResponseButton(dialog with { Buttons = [new(1, 6, "Yes"), new(2, 6, "Yes")] }));
    }

    [Fact]
    public void ChangedInputBlocksEvenAnExactRecoveryPrompt()
    {
        using var files = new TestFiles();
        var project = files.Project();
        var monitor = new OwnedDialogMonitor(new(files.Root), project, "Launch");
        File.AppendAllText(project, "changed");
        var process = new DialogProcess { Dialog = Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt) };
        var error = Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Contains("changed since launch", error.Message);
        Assert.Equal(0, process.Responses);
    }

    [Fact]
    public void InitializingCustomMessageHasBoundedGraceWithoutReceivingInput()
    {
        using var files = new TestFiles();
        var time = new ManualTime();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Launch", time);
        var process = new DialogProcess { Dialog = Dialog(100, "Invalid data", "") with { WindowClass = "TMsgForm" } };
        Assert.True(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(0, process.Responses);
        time.Now += TimeSpan.FromSeconds(2);
        Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(0, process.Responses);
    }

    [Fact]
    public void StartupSaveIsDeclinedAndRenderSaveRecoveryIsRefused()
    {
        var dialog = Dialog(100, "Invalid data", InvalidNotesDialog.SavePrompt);
        Assert.Equal(7, InvalidNotesDialog.ResponseButton(dialog, "Launch")?.Id);
        Assert.Null(InvalidNotesDialog.ResponseButton(dialog, "Render"));
        Assert.Equal(6, InvalidNotesDialog.ResponseButton(dialog with { Text = [InvalidNotesDialog.LoadPrompt] }, "Render")?.Id);
    }

    [Fact]
    public void CustomMessageRequiresVerifiedNativeChoiceMetadata()
    {
        var dialog = Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt) with { WindowClass = "TMsgForm" };
        Assert.Null(InvalidNotesDialog.ResponseButton(dialog));
        Assert.Equal(6, InvalidNotesDialog.ResponseButton(dialog with { VerifiedNativeChoices = true })?.Id);
    }

    [Fact]
    public void DistinctStartupSaveAndLoadPromptsAreBoundedAndReportedSeparately()
    {
        using var files = new TestFiles();
        var monitor = new OwnedDialogMonitor(new(files.Root), files.Project(), "Launch");
        var process = new DialogProcess { Dialog = Dialog(100, "Invalid data", InvalidNotesDialog.SavePrompt) };
        Assert.True(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(7, process.LastResponse);
        process.Dialog = Dialog(100, "Invalid data", InvalidNotesDialog.LoadPrompt);
        Assert.True(monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(6, process.LastResponse);
        Assert.Equal(new[] { "invalid-notes-save-skipped", "invalid-notes-recovery" }, monitor.Warnings.Select(warning => warning.Code));
        process.Dialog = Dialog(100, "Invalid data", InvalidNotesDialog.SavePrompt) with { Window = 555 };
        Assert.Throws<IOException>(() => monitor.Inspect(process, CancellationToken.None));
        Assert.Equal(2, process.Responses);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task LaunchDialogRefusalStopsOnlyDisposableProcessAndKeepsOriginal()
    {
        using var files = new TestFiles();
        var executable = files.PathFor("FL64.exe");
        File.WriteAllText(executable, "never executed");
        var host = new DialogHost { DialogOnLaunch = true };
        await using var session = new ManagedSession(new(executable, files.Project(), files.Root), host, new DialogBridge(host));
        var error = await Assert.ThrowsAsync<IOException>(() => session.LaunchAsync("fresh.flp", 1, CancellationToken.None));
        Assert.Contains("Unrecognized fixture", error.Message);
        Assert.True(Assert.Single(host.Started).Terminated);
        Assert.Equal(0, host.Started[0].Responses);
        var original = Assert.Single(Directory.GetFiles(files.Root, "original.flp", SearchOption.AllDirectories));
        Assert.Equal(File.ReadAllBytes(files.PathFor("template.flp")), File.ReadAllBytes(original));
    }

    [Fact]
    public async Task RenderDialogRefusalIsImmediateAndKeepsSnapshot()
    {
        using var files = new TestFiles();
        var executable = files.PathFor("FL64.exe");
        File.WriteAllText(executable, "never executed");
        var host = new DialogHost();
        await using var session = new ManagedSession(new(executable, files.Project(), files.Root), host, new DialogBridge(host));
        await session.LaunchAsync("fresh.flp", 1, CancellationToken.None);
        var error = await Assert.ThrowsAsync<IOException>(() => session.RenderAsync("mix.wav", 30, CancellationToken.None));
        Assert.Contains("Unrecognized fixture", error.Message);
        Assert.Contains("Snapshot preserved", error.Message);
        Assert.True(host.Started[1].Terminated);
        Assert.Equal(0, host.Started[1].Responses);
        Assert.True(File.Exists(host.RenderProject));
    }

    [Fact]
    public async Task SuccessfulLaunchRecoveryReturnsAndRetainsWarning()
    {
        using var files = new TestFiles();
        var executable = files.PathFor("FL64.exe");
        File.WriteAllText(executable, "never executed");
        var host = new DialogHost { RecoverLaunch = true };
        await using var session = new ManagedSession(new(executable, files.Project(), files.Root), host, new DialogBridge(host));
        var status = await session.LaunchAsync("fresh.flp", 3, CancellationToken.None);
        var warning = Assert.Single(status.Warnings);
        Assert.True(File.Exists(warning.OriginalProject));
        Assert.True(File.Exists(warning.Diagnostic));
        Assert.Equal(1, Assert.Single(host.Started).Responses);
        var later = await session.CallAsync("status", new { }, CancellationToken.None);
        Assert.Single(later.GetProperty("warnings").EnumerateArray());
    }

    private static StudioDialog Dialog(int pid, string title, string text) =>
        new(123, pid, "#32770", title, [text], [new(124, 6, "&Yes"), new(125, 7, "&No")]);

    private static StudioDialog RenderProgress(int pid) =>
        new(321, pid, "TWAVRenderForm", "Rendering to \ue409render.wav", [], []);

    private sealed class DialogProcess : IManagedProcess
    {
        public int Id { get; init; } = 100;
        public StudioDialog? Dialog { get; set; }
        public IReadOnlyList<StudioDialog>? Dialogs { get; init; }
        public int Responses { get; private set; }
        public int? LastResponse { get; private set; }
        public Action? BeforeResponse { get; set; }
        public bool Terminated { get; private set; }
        public bool HasExited => Terminated;
        public int ExitCode => 0;
        public Task WaitForExitAsync(CancellationToken ct) => Task.Delay(Timeout.Infinite, ct);
        public IReadOnlyList<StudioDialog> ReadDialogs(CancellationToken ct) => Dialogs ?? (Dialog is null ? [] : [Dialog]);
        public bool TryRespondToDialog(StudioDialog dialog, StudioDialogButton button, CancellationToken ct) { BeforeResponse?.Invoke(); Responses++; LastResponse = button.Id; Dialog = null; return true; }
        public void Terminate() => Terminated = true;
        public void Dispose() { }
    }

    private sealed class DialogHost : IProcessHost
    {
        public bool DialogOnLaunch { get; init; }
        public bool RecoverLaunch { get; init; }
        public string Project { get; private set; } = "";
        public string? RenderProject { get; private set; }
        public List<DialogProcess> Started { get; } = [];
        public bool HasRunningStudio() => false;
        public IManagedProcess Start(ProcessStartInfo startInfo, bool background = false, CancellationToken ct = default)
        {
            var render = startInfo.ArgumentList[0] == "/R";
            if (render) RenderProject = startInfo.ArgumentList[^1];
            else Project = startInfo.ArgumentList[0];
            var process = new DialogProcess { Id = 100 + Started.Count };
            if (render || DialogOnLaunch) process.Dialog = Dialog(process.Id, "Unrecognized fixture", "Do not click this dialog.");
            if (!render && RecoverLaunch) process.Dialog = Dialog(process.Id, "Invalid data", InvalidNotesDialog.LoadPrompt);
            Started.Add(process);
            return process;
        }
    }

    private sealed class DialogBridge(DialogHost host) : IBridgeClient
    {
        public Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
        {
            if (operation == "save") TestFiles.WriteProject(((PathArgs)arguments).Path);
            return Task.FromResult(Messages.Element(new SessionStatus(true, processId, "", 120, 96) { ProjectPath = host.Project }));
        }
    }
}
