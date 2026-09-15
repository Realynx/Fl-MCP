using System.Diagnostics;
using System.Text.Json;
using FlMcp.Protocol;
using FlMcp.Server;

namespace FlMcp.Tests;

/// <summary>A managed session over a fake process host and a fake bridge: FL is never started. Shared by the
/// session, render and audio-tool tests.</summary>
internal sealed class SessionFixture : IDisposable
{
    public TestFiles Files { get; } = new();
    public FakeProcesses Processes { get; } = new();
    public FakeBridge Bridge { get; } = new();
    public ServerSettings Settings { get; }
    public SessionFixture()
    {
        var executable = Files.PathFor("FL64.exe");
        File.WriteAllText(executable, "test-only placeholder; never executed");
        Settings = new(executable, Files.Project(), Files.Root)
        {
            ProjectSettleWindow = TimeSpan.FromMilliseconds(20),
            ProjectSettlePollInterval = TimeSpan.FromMilliseconds(5),
            ProjectSettleTimeout = TimeSpan.FromSeconds(2),
            HostLogDirectory = Files.Root,
        };
        Processes.OnStart = info => Bridge.Project = info.ArgumentList[0];
    }
    public ManagedSession Session() => new(Settings, Processes, Bridge);
    public void Dispose() => Files.Dispose();
}

internal sealed class FakeBridge : IBridgeClient
{
    public bool Available { get; set; } = true;
    public bool ValidSave { get; set; } = true;
    /// <summary>Tempos reported by successive status calls; the last value repeats.</summary>
    public Queue<double> TempoSequence { get; } = new();
    /// <summary>Answer for the "song" operation; null behaves like a plugin build without it.</summary>
    public SongExtent? Song { get; set; }
    public string Project { get; set; } = "";
    public string? StructuredProject { get; set; }
    public List<string> Calls { get; } = [];
    public Func<PythonExecute, CancellationToken, Task<JsonElement>>? OnPython { get; set; }
    public Task<JsonElement> CallAsync(int processId, string token, string operation, object arguments, int timeoutSeconds, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls.Add(operation);
        if (operation == "python_execute") return OnPython!((PythonExecute)arguments, ct);
        if (operation == "song" && Song is not null) return Task.FromResult(Messages.Element(Song));
        if (operation == "python_call") return Task.FromResult(Messages.Element(new PythonReply(Messages.Element(new { apiVersion = 1, operations = Array.Empty<object>() }))));
        if (operation == "save")
        {
            var path = ((PathArgs)arguments).Path;
            if (ValidSave) TestFiles.WriteProject(path);
            else File.WriteAllText(path, "this is not a valid project file");
            return Task.FromResult(Messages.Element(new { path }));
        }
        var tempo = TempoSequence.Count switch { 0 => 120, 1 => TempoSequence.Peek(), _ => TempoSequence.Dequeue() };
        return Task.FromResult(Messages.Element(new SessionStatus(Available, processId, "Title: fixture\nPath: " + Project + "\nSaved: yes", tempo, 96) { ProjectPath = StructuredProject }));
    }
}

internal sealed class FakeProcesses : IProcessHost
{
    public bool ExistingStudio { get; set; }
    public bool HangRender { get; set; }
    public bool BlockStart { get; set; }
    /// <summary>Exit codes for successive started processes; 0 once exhausted.</summary>
    public Queue<int> ExitCodes { get; } = new();
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
        }, ExitCodes.Count > 0 ? ExitCodes.Dequeue() : 0);
        Started.Add((info, process, background));
        return process;
    }
}

internal sealed class FakeProcess(int id, Func<CancellationToken, Task> wait, int exitCode = 0) : IManagedProcess
{
    public int Id => id;
    public bool Terminated { get; private set; }
    public int StartupCompletions { get; private set; }
    public bool HasExited => Terminated;
    public int ExitCode => exitCode;
    public Task WaitForExitAsync(CancellationToken ct) => wait(ct);
    public void CompleteStartup() => StartupCompletions++;
    public void Terminate() => Terminated = true;
    public void Dispose() { }
}
