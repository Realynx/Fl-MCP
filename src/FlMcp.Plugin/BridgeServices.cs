using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FlMcp.Tests")]

namespace FlMcp.Plugin;

/// <summary>Runs listeners as one lifetime, retaining ownership until every listener has drained.</summary>
internal static class BridgeServices
{
    internal static async Task RunAsync(CancellationToken ct, params Func<CancellationToken, Task>[] services)
    {
        ArgumentOutOfRangeException.ThrowIfZero(services.Length);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = services.Select(service => StartAsync(service, lifetime.Token)).ToArray();
        await Task.WhenAny(tasks).ConfigureAwait(false);
        var stopping = ct.IsCancellationRequested;
        try { await lifetime.CancelAsync().ConfigureAwait(false); }
        finally
        {
            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch (OperationCanceledException) when (stopping) { }
        }
        if (!stopping) throw new InvalidOperationException("An MCP listener stopped unexpectedly; its companion listeners have been drained.");
    }

    private static async Task StartAsync(Func<CancellationToken, Task> service, CancellationToken ct)
        => await service(ct).ConfigureAwait(false);
}
