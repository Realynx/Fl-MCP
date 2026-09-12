using System.IO.Pipes;
using FlMcp.Plugin;
using FlMcp.Protocol;
using Xunit;

namespace FlMcp.Tests;

public sealed class PipeTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1048577)]
    public async Task RejectsOversizedOrInvalidFrameBeforeAllocation(int length)
    {
        using var stream = new MemoryStream(BitConverter.GetBytes(length));
        await Assert.ThrowsAsync<InvalidDataException>(() => PipeProtocol.ReadAsync<BridgeRequest>(stream, TestContextToken()));
    }

    [Fact]
    public async Task RejectsTruncatedPayload()
    {
        using var stream = new MemoryStream([10, 0, 0, 0, 123]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => PipeProtocol.ReadAsync<BridgeRequest>(stream, TestContextToken()));
    }

    [Fact]
    public async Task AuthenticatesAndRecoversAfterRejectedConnection()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = "fl-mcp-test-" + Guid.NewGuid().ToString("N");
        var calls = 0;
        var server = new BridgeServer(name, new string('a', 64), (_, _, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Messages.Element(new { ready = true }));
        }, _ => { });
        var running = server.RunAsync(deadline.Token);
        var denied = await RequestAsync(name, "wrong", deadline.Token);
        Assert.NotNull(denied.Error);
        Assert.Equal(0, calls);
        var allowed = await RequestAsync(name, new string('a', 64), deadline.Token);
        Assert.Null(allowed.Error);
        Assert.Equal(1, calls);
        await deadline.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ShutdownCancelsStalledClientRead()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var name = "fl-mcp-test-" + Guid.NewGuid().ToString("N");
        var server = new BridgeServer(name, "token", (_, _, _) => throw new InvalidOperationException(), _ => { });
        var running = server.RunAsync(deadline.Token);
        await using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(deadline.Token);
        await deadline.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static async Task<BridgeResponse> RequestAsync(string name, string token, CancellationToken ct)
    {
        await using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(ct);
        await PipeProtocol.WriteAsync(pipe, new BridgeRequest(token, "status", Messages.Element(new { })), ct);
        return await PipeProtocol.ReadAsync<BridgeResponse>(pipe, ct);
    }

    [Fact]
    public async Task ClientDisconnectCancelsActiveOperation()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var name = "fl-mcp-test-" + Guid.NewGuid().ToString("N");
        var server = new BridgeServer(name, "secret", async (_, _, ct) =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { cancelled.SetResult(); throw; }
            return Messages.Element(new { });
        }, _ => { });
        var running = server.RunAsync(deadline.Token);
        await using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await pipe.ConnectAsync(deadline.Token);
            await PipeProtocol.WriteAsync(pipe, new BridgeRequest("secret", "wait", Messages.Element(new { })), deadline.Token);
            await entered.Task.WaitAsync(deadline.Token);
        }
        await cancelled.Task.WaitAsync(deadline.Token);
        await deadline.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static CancellationToken TestContextToken() => CancellationToken.None;
}
