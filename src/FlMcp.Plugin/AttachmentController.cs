using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;

namespace FlMcp.Plugin;

/// <summary>One observing client lease in an ordinary FL process; never owns or stops that process.</summary>
public sealed class AttachmentController : IAsyncDisposable
{
    private readonly string instanceId;
    private readonly string executable;
    private readonly Func<int, long?> processStamp;
    private readonly Func<WorkspacePaths, EmbeddedPythonOptions, CommandDispatcher> createDispatcher;
    private Lease? lease;
    private EmbeddedPythonOptions? initializedPython;

    public AttachmentController(INativeFlControl fl, string instanceId, string executable,
        Func<int, long?>? processStamp = null,
        Func<WorkspacePaths, EmbeddedPythonOptions, CommandDispatcher>? createDispatcher = null)
    {
        this.instanceId = instanceId;
        this.executable = executable;
        this.processStamp = processStamp ?? ReadProcessStamp;
        this.createDispatcher = createDispatcher ?? ((paths, options) =>
            new CommandDispatcher(fl, paths, handler =>
            {
                initializedPython = options;
                return new EmbeddedPythonRuntime(options, handler);
            }));
    }

    public async Task<JsonElement> DispatchAsync(int peer, BridgeRequest request, CancellationToken ct)
    {
        if (request.Operation == "attach")
            return Messages.Element(await AttachAsync(peer, Messages.Arguments<AttachRequest>(request.Arguments), ct).ConfigureAwait(false));
        var current = RequireLease(peer, request.LeaseToken);
        if (request.Operation == "detach")
        {
            await ReleaseAsync().ConfigureAwait(false);
            return Messages.Element(new { detached = true, processPreserved = true });
        }
        var status = await StatusAsync(current.Dispatcher, ct).ConfigureAwait(false);
        var changed = !current.Project.Matches(ProjectIdentity.FromStatus(status));
        if (request.Operation == "status")
            return Messages.Element(status with { Ownership = "attached", RequiresReattach = changed });
        if (changed) throw new InvalidOperationException("The attached project changed. Call fl_attach again before editing it.");
        if (request.Operation == "save") throw new InvalidOperationException("Attached sessions use snapshot saves; managed playback changes are not allowed.");
        var arguments = request.Arguments;
        if (request.Operation == "python_execute")
        {
            var script = Messages.Arguments<PythonExecute>(arguments);
            arguments = Messages.Element(script with { ExpectedProjectPath = current.Project.Path, AttachedProject = current.Project });
        }
        return await current.Dispatcher.DispatchAsync(request.Operation, arguments, ct).ConfigureAwait(false);
    }

    private async Task<AttachReply> AttachAsync(int peer, AttachRequest request, CancellationToken ct)
    {
        if (request.InstanceId != instanceId) throw new InvalidOperationException("FL MCP was reloaded. Refresh fl_instances and attach again.");
        var stamp = processStamp(peer) ?? throw new IOException("The requesting MCP process is no longer running.");
        if (lease is { } existing && processStamp(existing.Peer) == existing.Started && (existing.Peer != peer || existing.Started != stamp))
            throw new InvalidOperationException("This FL instance is attached to another MCP client. Detach it there first.");
        var options = RuntimeOptions(request);
        if (initializedPython is not null && !SameOptions(initializedPython, options))
            throw new InvalidOperationException("FL already initialized another Python configuration. Restart FL to change its private runtime.");
        var paths = new WorkspacePaths(request.Workspace);
        await ReleaseAsync().ConfigureAwait(false);
        var dispatcher = createDispatcher(paths, options);
        try
        {
            var status = await StatusAsync(dispatcher, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            lease = new(peer, stamp, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), paths.Root,
                ProjectIdentity.FromStatus(status), dispatcher);
            return new(instanceId, lease.Token, lease.Workspace, status with { Ownership = "attached" });
        }
        catch { await dispatcher.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private Lease RequireLease(int peer, string? token)
    {
        if (lease is null || lease.Peer != peer || processStamp(peer) != lease.Started || !PipeProtocol.TokenMatches(lease.Token, token))
            throw new InvalidOperationException("No active attachment belongs to this MCP client. Call fl_attach first.");
        return lease;
    }

    private static async Task<SessionStatus> StatusAsync(CommandDispatcher dispatcher, CancellationToken ct)
    {
        var value = await dispatcher.DispatchAsync("status", Messages.Element(new { }), ct).ConfigureAwait(false);
        var status = value.Deserialize<SessionStatus>(Messages.Json);
        if (status is not { Available: true }) throw new InvalidOperationException("FL is not ready for attachment. Wait for startup to finish and retry.");
        return status;
    }

    private EmbeddedPythonOptions RuntimeOptions(AttachRequest request)
    {
        var root = Path.Combine(Path.GetDirectoryName(executable)!, "FruityLink", "tools", "fl-mcp", "python");
        var runtime = request.PythonRuntimeDirectory ?? Path.Combine(root, "runtime");
        var package = request.PythonPackagePath ?? Path.Combine(root, "fruitylink_python-0.2.0-py3-none-any.whl");
        if (!Path.IsPathFullyQualified(runtime) || !Path.IsPathFullyQualified(package))
            throw new ArgumentException("Embedded Python runtime and package paths must be absolute.");
        return new(Path.GetFullPath(runtime), Path.GetFullPath(package));
    }

    private static bool SameOptions(EmbeddedPythonOptions first, EmbeddedPythonOptions second) =>
        string.Equals(first.RuntimeDirectory, second.RuntimeDirectory, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(first.PythonPackagePath, second.PythonPackagePath, StringComparison.OrdinalIgnoreCase);

    private static long? ReadProcessStamp(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.StartTime.ToUniversalTime().Ticks;
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    private async Task ReleaseAsync()
    {
        if (lease is not null) await lease.Dispatcher.DisposeAsync().ConfigureAwait(false);
        lease = null;
    }

    public ValueTask DisposeAsync() => new(ReleaseAsync());

    private sealed record Lease(int Peer, long Started, string Token, string Workspace, ProjectIdentity Project, CommandDispatcher Dispatcher);
}
