using FlMcp.Protocol;
using FruityLink.Plugins.Abstractions;
using System.Security.Cryptography;

namespace FlMcp.Plugin;

/// <summary>A UI-free bridge for managed projects and explicit attachment to ordinary FL sessions.</summary>
public sealed class FlMcpPlugin : IFlPlugin
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private CancellationTokenSource? lifetime;
    private Task? server;
    private CommandDispatcher? dispatcher;
    private AttachmentController? attachment;

    public string Id => "fl-mcp";
    public string Name => "FL MCP";
    public string Description => "Local MCP control for attached or disposable FL Studio sessions.";
    public string Version => "0.2.0";

    public async Task EnableAsync(IPluginContext context, CancellationToken ct = default)
    {
        await lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (server is not null) return;
            var token = Environment.GetEnvironmentVariable(PipeProtocol.TokenVariable);
            var root = Environment.GetEnvironmentVariable(PipeProtocol.WorkspaceVariable);
            lifetime = new CancellationTokenSource();
            if (token?.Length == 64 && !string.IsNullOrWhiteSpace(root))
            {
                dispatcher = new CommandDispatcher(context.Fl, new WorkspacePaths(root));
                var bridge = new BridgeServer(PipeProtocol.Name(Environment.ProcessId), token, dispatcher.DispatchAsync, context.Log);
                server = BridgeServices.RunAsync(lifetime.Token, bridge.RunAsync);
                context.Log("[fl-mcp] Managed session bridge enabled.");
            }
            else EnableAttachment(context, lifetime.Token);
            if (server!.IsCompleted) await server.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Log($"[fl-mcp] Enable failed: {ex.Message}");
            try { await CleanupAsync().ConfigureAwait(false); }
            catch (Exception cleanupError) { throw new AggregateException("MCP startup and cleanup failed.", ex, cleanupError); }
            throw;
        }
        finally { lifecycle.Release(); }
    }

    private void EnableAttachment(IPluginContext context, CancellationToken ct)
    {
        var endpoint = new FlInstanceEndpoint(Environment.ProcessId, Guid.NewGuid().ToString("D"),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate this FL executable."));
        attachment = new AttachmentController(context.Fl, endpoint.InstanceId, endpoint.ExecutablePath);
        var bridge = new BridgeServer(PipeProtocol.Name(endpoint.ProcessId), endpoint.Token,
            (_, _, _) => throw new InvalidOperationException("An attachment lease is required."), context.Log, attachment.DispatchAsync);
        server = BridgeServices.RunAsync(ct, bridge.RunAsync, new InstanceDiscoveryServer(endpoint).RunAsync);
        context.Log("[fl-mcp] Ready for an explicit MCP attachment to this FL session.");
    }

    public async Task DisableAsync(CancellationToken ct = default)
    {
        // Cleanup must finish even if the caller's shutdown token has already expired.
        await lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            await CleanupAsync().ConfigureAwait(false);
        }
        finally { lifecycle.Release(); }
    }

    private async Task CleanupAsync()
    {
        var errors = new List<Exception>();
        try
        {
            if (lifetime is not null) await CollectErrorAsync(lifetime.CancelAsync, errors).ConfigureAwait(false);
            if (server is not null) await CollectErrorAsync(DrainServerAsync, errors).ConfigureAwait(false);
            if (dispatcher is not null) await CollectErrorAsync(() => dispatcher.DisposeAsync().AsTask(), errors).ConfigureAwait(false);
            if (attachment is not null) await CollectErrorAsync(() => attachment.DisposeAsync().AsTask(), errors).ConfigureAwait(false);
        }
        finally
        {
            lifetime?.Dispose();
            lifetime = null;
            server = null;
            dispatcher = null;
            attachment = null;
        }
        if (errors.Count != 0) throw new AggregateException("MCP services stopped with errors after cleanup completed.", errors);
    }

    private async Task DrainServerAsync()
    {
        try { await server!.ConfigureAwait(false); }
        catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true) { }
    }

    private static async Task CollectErrorAsync(Func<Task> action, List<Exception> errors)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception ex) { errors.Add(ex); }
    }
}
