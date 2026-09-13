using System.Text.Json;
using FlMcp.Protocol;

namespace FlMcp.Server;

public interface IInstanceSource
{
    Task<FlInstanceEndpoint> ReadAsync(int processId, CancellationToken ct);
}

public sealed class PipeInstanceSource : IInstanceSource
{
    public Task<FlInstanceEndpoint> ReadAsync(int processId, CancellationToken ct) => InstanceDiscovery.ReadAsync(processId, ct);
}

public sealed record AvailableInstance(int ProcessId, string InstanceId, string ExecutablePath);
public sealed record AttachedSession(int ProcessId, string Ownership, string Workspace, SessionStatus Status);

public sealed partial class ManagedSession
{
    private enum SessionOwnership { None, Owned, Attached }
    private readonly IInstanceSource instances = discovery ?? new PipeInstanceSource();
    private SessionOwnership ownership;
    private string? leaseToken;
    private ProjectIdentity? attachedProject;
    private WorkspacePaths? attachmentPaths;

    public async Task<IReadOnlyList<AvailableInstance>> ListInstancesAsync(CancellationToken ct)
    {
        RequireConfiguredExecutable();
        var probes = processes.ListStudioProcessIds().Select(pid => ProbeInstanceAsync(pid, ct));
        var found = await Task.WhenAll(probes).ConfigureAwait(false);
        return found.Where(item => item is not null).Select(item => item!).ToArray();
    }

    private async Task<AvailableInstance?> ProbeInstanceAsync(int pid, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            var endpoint = await instances.ReadAsync(pid, deadline.Token).ConfigureAwait(false);
            return MatchesExecutable(endpoint.ExecutablePath) ? new(endpoint.ProcessId, endpoint.InstanceId, endpoint.ExecutablePath) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return null; }
        catch (IOException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    public async Task<AttachedSession> AttachAsync(int processId, CancellationToken ct)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        RequireConfiguredExecutable();
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (ownership == SessionOwnership.Owned && authoring is { HasExited: false })
                throw new InvalidOperationException("A disposable managed project is open. Save/close it before attaching to another FL session.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var endpoint = await instances.ReadAsync(processId, deadline.Token).ConfigureAwait(false);
            if (endpoint.ProcessId != processId || !MatchesExecutable(endpoint.ExecutablePath))
                throw new InvalidOperationException("The discovered FL executable does not match this MCP client's FL_MCP_FL_EXE configuration.");
            return await BindAttachmentAsync(endpoint, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("FL attachment did not finish before its deadline. Wait for project loading and any previous operation to finish, then retry fl_attach.");
        }
        finally { gate.Release(); }
    }

    private async Task<AttachedSession> BindAttachmentAsync(FlInstanceEndpoint endpoint, CancellationToken ct)
    {
        if (ownership == SessionOwnership.Attached && authoring?.Id != endpoint.ProcessId)
            await DetachCoreAsync(ct).ConfigureAwait(false);
        var observed = processes.Observe(endpoint.ProcessId);
        try
        {
            var request = new AttachRequest(endpoint.InstanceId, paths.Root, settings.PythonRuntimeDirectory, settings.PythonPackagePath);
            var result = await bridge.CallAsync(endpoint.ProcessId, endpoint.Token, "attach", request, 15, ct).ConfigureAwait(false);
            var reply = result.Deserialize<AttachReply>(Messages.Json) ?? throw new InvalidDataException("FL returned no attachment lease.");
            ValidateReply(endpoint, reply);
            if (!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(reply.Workspace)),
                Path.TrimEndingDirectorySeparator(paths.Root), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("FL returned a different attachment workspace. Retry fl_attach with the configured workspace.");
            authoring?.Dispose();
            authoring = observed;
            token = endpoint.Token;
            leaseToken = reply.LeaseToken;
            attachedProject = ProjectIdentity.FromStatus(reply.Status);
            expectedProject = null;
            attachmentPaths = paths;
            ownership = SessionOwnership.Attached;
            embeddedCompletionUnknown = false;
            launchWarnings = [];
            return new(endpoint.ProcessId, "attached", attachmentPaths.Root, reply.Status);
        }
        catch { observed.Dispose(); throw; }
    }

    private static void ValidateReply(FlInstanceEndpoint endpoint, AttachReply reply)
    {
        if (reply.InstanceId != endpoint.InstanceId || reply.Status is not { Available: true } ||
            reply.Status.ProcessId != endpoint.ProcessId || reply.LeaseToken?.Length != 64 || !Path.IsPathFullyQualified(reply.Workspace))
            throw new InvalidDataException("FL attachment identity or lease response was invalid.");
    }

    public async Task<object> DetachAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (ownership == SessionOwnership.Owned)
                throw new InvalidOperationException("This session owns a disposable FL project. Save/close it with fl_project_close.");
            await DetachCoreAsync(ct).ConfigureAwait(false);
            return new { detached = true, processPreserved = true };
        }
        finally { gate.Release(); }
    }

    private async Task DetachCoreAsync(CancellationToken ct)
    {
        if (ownership == SessionOwnership.Attached && authoring is { HasExited: false })
            await CallCoreAsync("detach", new { }, 5, ct).ConfigureAwait(false);
        ResetAttachment();
    }

    private void ResetAttachment()
    {
        authoring?.Dispose();
        authoring = null;
        token = null;
        leaseToken = null;
        expectedProject = null;
        attachedProject = null;
        attachmentPaths = null;
        ownership = SessionOwnership.None;
        embeddedCompletionUnknown = false;
    }

    private void RequireOwnedLifecycle(string operation)
    {
        if (ownership == SessionOwnership.Attached)
            throw new InvalidOperationException($"Cannot {operation} a user-owned attached FL session. Save a snapshot or fl_detach; FL will remain open.");
    }

    private void RequireConfiguredExecutable()
    {
        if (string.IsNullOrWhiteSpace(settings.Executable) || !Path.IsPathFullyQualified(settings.Executable))
            throw new InvalidOperationException("Set FL_MCP_FL_EXE to the FL installation this client should attach to.");
    }

    private bool MatchesExecutable(string executable) => Path.IsPathFullyQualified(executable) &&
        string.Equals(Path.GetFullPath(executable), Path.GetFullPath(settings.Executable!), StringComparison.OrdinalIgnoreCase);
}
