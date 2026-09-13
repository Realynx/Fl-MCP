using System.Reflection;
using FlMcp.Plugin;
using Xunit;

namespace FlMcp.Tests;

public sealed class BridgeServicesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FaultCancelsSiblingButWaitsForItsActualDrain(bool synchronousFailure)
    {
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new IOException("fake listener failure");
        Task Fail(CancellationToken _) => synchronousFailure ? throw expected : failed.Task;
        async Task Sibling(CancellationToken ct)
        {
            using var registration = ct.Register(() => cancelled.TrySetResult());
            await cancelled.Task;
            await drained.Task;
        }
        var services = BridgeServices.RunAsync(CancellationToken.None, Sibling, Fail);
        if (!synchronousFailure) failed.SetException(expected);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(services.IsCompleted);
        drained.SetResult();
        Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => services));
    }

    [Fact]
    public async Task ShutdownCancellationDrainsEveryService()
    {
        using var lifetime = new CancellationTokenSource();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Slow(CancellationToken ct)
        {
            using var registration = ct.Register(() => cancelled.TrySetResult());
            await cancelled.Task;
            await release.Task;
        }
        var services = BridgeServices.RunAsync(lifetime.Token, ct => Task.Delay(Timeout.Infinite, ct), Slow);
        await lifetime.CancelAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(services.IsCompleted);
        release.SetResult();
        await services.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UnexpectedListenerReturnCancelsCompanionAndFailsGroup()
    {
        var cancelled = false;
        async Task Companion(CancellationToken ct)
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { cancelled = true; }
        }
        await Assert.ThrowsAnyAsync<Exception>(() => BridgeServices.RunAsync(CancellationToken.None,
            Companion, _ => Task.CompletedTask));
        Assert.True(cancelled);
    }

    [Fact]
    public async Task DisableAfterServerFaultCancelsLifetimeAndResetsState()
    {
        var plugin = new FlMcpPlugin();
        var lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        SetField(plugin, "lifetime", lifetime);
        SetField(plugin, "server", Task.FromException(new IOException("fake server failure")));
        await Assert.ThrowsAsync<AggregateException>(() => plugin.DisableAsync(new CancellationToken(true)));
        Assert.True(token.IsCancellationRequested);
        foreach (var field in new[] { "lifetime", "server", "dispatcher", "attachment" })
            Assert.Null(typeof(FlMcpPlugin).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plugin));
        await plugin.DisableAsync();
    }

    private static void SetField(FlMcpPlugin plugin, string field, object value)
        => typeof(FlMcpPlugin).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, value);
}
