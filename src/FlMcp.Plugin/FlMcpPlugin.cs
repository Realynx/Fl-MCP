using FlMcp.Protocol;
using FruityLink.Plugins.Abstractions;

namespace FlMcp.Plugin;

/// <summary>A UI-free, session-scoped FruityLink plugin. It is inert outside a companion-owned FL process.</summary>
public sealed class FlMcpPlugin : IFlPlugin
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private CancellationTokenSource? lifetime;
    private Task? server;

    public string Id => "fl-mcp";
    public string Name => "FL MCP";
    public string Description => "Local MCP control for an isolated FL Studio authoring session.";
    public string Version => "0.1.0";

    public async Task EnableAsync(IPluginContext context, CancellationToken ct = default)
    {
        await lifecycle.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (server is not null) return;
            var token = Environment.GetEnvironmentVariable(PipeProtocol.TokenVariable);
            var root = Environment.GetEnvironmentVariable(PipeProtocol.WorkspaceVariable);
            if (token?.Length != 64 || string.IsNullOrWhiteSpace(root))
            {
                context.Log("[fl-mcp] Idle: start a managed session through the FL MCP companion.");
                return;
            }
            var dispatcher = new CommandDispatcher(context.Fl, new WorkspacePaths(root));
            lifetime = new CancellationTokenSource();
            var bridge = new BridgeServer(PipeProtocol.Name(Environment.ProcessId), token, dispatcher.DispatchAsync, context.Log);
            server = bridge.RunAsync(lifetime.Token);
            context.Log("[fl-mcp] Managed session bridge enabled.");
        }
        catch (Exception ex)
        {
            lifetime?.Dispose();
            lifetime = null;
            context.Log($"[fl-mcp] Enable failed: {ex.Message}");
        }
        finally { lifecycle.Release(); }
    }

    public async Task DisableAsync(CancellationToken ct = default)
    {
        // Cleanup must finish even if the caller's shutdown token has already expired.
        await lifecycle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (lifetime is null) return;
            await lifetime.CancelAsync().ConfigureAwait(false);
            if (server is not null) await server.ConfigureAwait(false);
            lifetime.Dispose();
            lifetime = null;
            server = null;
        }
        finally { lifecycle.Release(); }
    }
}
