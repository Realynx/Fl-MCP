using System.Collections;
using System.Diagnostics;
using FruityLink.Core.Hosting;

namespace FlMcp.Server;

public interface IManagedProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken ct);
    IReadOnlyList<StudioDialog> ReadDialogs(CancellationToken ct) => [];
    bool TryRespondToDialog(StudioDialog dialog, StudioDialogButton button, CancellationToken ct) => false;
    void CompleteStartup() { }
    void Terminate();
}

public interface IProcessHost
{
    bool HasRunningStudio();
    IManagedProcess Start(ProcessStartInfo startInfo, bool background = false, CancellationToken ct = default);
    IReadOnlyList<int> ListStudioProcessIds() => [];
    IManagedProcess Observe(int processId) => throw new NotSupportedException("This host cannot observe existing processes.");
}

public sealed class ProcessHost : IProcessHost
{
    public IReadOnlyList<int> ListStudioProcessIds()
    {
        var matches = Process.GetProcessesByName("FL").Concat(Process.GetProcessesByName("FL64")).ToArray();
        try { return matches.Select(process => process.Id).ToArray(); }
        finally { foreach (var process in matches) process.Dispose(); }
    }

    public IManagedProcess Observe(int processId) => new ObservedProcess(Process.GetProcessById(processId));
    public bool HasRunningStudio()
    {
        var processes = Process.GetProcessesByName("FL").Concat(Process.GetProcessesByName("FL64")).ToArray();
        var found = processes.Length > 0;
        foreach (var process in processes) process.Dispose();
        return found;
    }

    public IManagedProcess Start(ProcessStartInfo startInfo, bool background = false, CancellationToken ct = default) => background
        ? new BackgroundManagedProcess(FlStudioProcessLauncher.Start(ToLaunchOptions(startInfo), ct))
        : new InteractiveManagedProcess(Process.Start(startInfo) ?? throw new IOException("FL Studio did not start."));

    private static FlStudioLaunchOptions ToLaunchOptions(ProcessStartInfo startInfo)
    {
        var environment = startInfo.Environment.ToDictionary(pair => pair.Key, pair => (string?)pair.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry inherited in Environment.GetEnvironmentVariables())
        {
            var name = (string)inherited.Key;
            if (!startInfo.Environment.ContainsKey(name)) environment[name] = null;
        }
        return new(startInfo.FileName)
        {
            Arguments = startInfo.ArgumentList.ToArray(),
            WorkingDirectory = startInfo.WorkingDirectory,
            Environment = environment,
            Mode = FlStudioLaunchMode.PrivateDesktop,
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        };
    }

    private sealed class BackgroundManagedProcess(FlStudioProcessLease process) : IManagedProcess
    {
        public int Id => process.ProcessId;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode ?? throw new InvalidOperationException("FL Studio has not exited.");
        public Task WaitForExitAsync(CancellationToken ct) => process.WaitForExitAsync(ct);
        public IReadOnlyList<StudioDialog> ReadDialogs(CancellationToken ct) =>
            process.HasExited ? [] : WindowsStudioDialogs.Read(process.ProcessId, process.ReadWindows(ct), ct);
        public bool TryRespondToDialog(StudioDialog dialog, StudioDialogButton button, CancellationToken ct) =>
            !process.HasExited && WindowsStudioDialogs.TryClick(process.ProcessId, dialog, button, ct);
        public void CompleteStartup() => process.CompleteStartup();
        public void Terminate()
        {
            if (process.HasExited) return;
            process.TerminateAsync().GetAwaiter().GetResult();
        }
        public void Dispose() => process.Dispose();
    }

    private sealed class InteractiveManagedProcess(Process process) : IManagedProcess
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken ct) => process.WaitForExitAsync(ct);
        public IReadOnlyList<StudioDialog> ReadDialogs(CancellationToken ct) =>
            process.HasExited ? [] : WindowsStudioDialogs.Read(process.Id, ct);
        public bool TryRespondToDialog(StudioDialog dialog, StudioDialogButton button, CancellationToken ct) =>
            !process.HasExited && WindowsStudioDialogs.TryClick(process.Id, dialog, button, ct);
        public void Terminate()
        {
            if (process.HasExited) return;
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000)) throw new TimeoutException("Owned FL Studio process did not stop within five seconds.");
        }
        public void Dispose() => process.Dispose();
    }

    private sealed class ObservedProcess(Process process) : IManagedProcess
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken ct) => process.WaitForExitAsync(ct);
        public void Terminate() => throw new InvalidOperationException("An attached FL process is owned by the user and cannot be terminated.");
        public void Dispose() => process.Dispose();
    }
}
