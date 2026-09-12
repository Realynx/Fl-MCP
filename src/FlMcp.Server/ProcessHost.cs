using System.Diagnostics;

namespace FlMcp.Server;

public interface IManagedProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync(CancellationToken ct);
    void Terminate();
}

public interface IProcessHost
{
    bool HasRunningStudio();
    IManagedProcess Start(ProcessStartInfo startInfo);
}

public sealed class ProcessHost : IProcessHost
{
    public bool HasRunningStudio()
    {
        var processes = Process.GetProcessesByName("FL").Concat(Process.GetProcessesByName("FL64")).ToArray();
        var found = processes.Length > 0;
        foreach (var process in processes) process.Dispose();
        return found;
    }

    public IManagedProcess Start(ProcessStartInfo startInfo) =>
        new ManagedProcess(Process.Start(startInfo) ?? throw new IOException("FL Studio did not start."));

    private sealed class ManagedProcess(Process process) : IManagedProcess
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public Task WaitForExitAsync(CancellationToken ct) => process.WaitForExitAsync(ct);
        public void Terminate()
        {
            if (process.HasExited) return;
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(5000)) throw new TimeoutException("Owned FL Studio process did not stop within five seconds.");
        }
        public void Dispose() => process.Dispose();
    }
}
