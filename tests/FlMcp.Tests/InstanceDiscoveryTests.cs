using System.IO.Pipes;
using FlMcp.Protocol;
using Xunit;

namespace FlMcp.Tests;

public sealed class InstanceDiscoveryTests
{
    [Fact]
    public async Task PublishesEndpointAndSurvivesSequentialConnections()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var endpoint = ValidEndpoint();
        var server = new InstanceDiscoveryServer(endpoint);
        var running = server.RunAsync(lifetime.Token);
        try
        {
            for (int index = 0; index < 30; index++)
                Assert.Equal(endpoint, await InstanceDiscovery.ReadAsync(Environment.ProcessId, lifetime.Token));
            Assert.False(running.IsCompleted);
        }
        finally { await lifetime.CancelAsync(); await running.WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public async Task VerifiesBothPeerIdentitiesUsingWindows()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var name = "fl-mcp-peer-test-" + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var accepted = server.WaitForConnectionAsync(deadline.Token);
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(deadline.Token);
        await accepted;
        Assert.Equal(Environment.ProcessId, PipePeerIdentity.GetServerProcessId(client));
        Assert.Equal(Environment.ProcessId, PipePeerIdentity.GetClientProcessId(server));
    }

    [Fact]
    public async Task RejectsSpoofedServerBeforeWaitingForAnyTokenFrame()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        const int impossiblePid = int.MaxValue;
        await using var spoof = new NamedPipeServerStream(InstanceDiscovery.Name(impossiblePid), PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var accepted = spoof.WaitForConnectionAsync(deadline.Token);
        // The server deliberately sends nothing. An implementation that reads before
        // verifying the OS identity would wait until cancellation instead of rejecting.
        var read = InstanceDiscovery.ReadAsync(impossiblePid, deadline.Token);
        await accepted;
        var error = await Assert.ThrowsAsync<IOException>(() => read.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Contains("different process", error.Message);
    }

    [Fact]
    public async Task RejectsDuplicatePublishersAndCanRestartAfterShutdown()
    {
        var endpoint = ValidEndpoint();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var running = new InstanceDiscoveryServer(endpoint).RunAsync(lifetime.Token);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new InstanceDiscoveryServer(endpoint).RunAsync(lifetime.Token));
            Assert.Equal(endpoint, await InstanceDiscovery.ReadAsync(Environment.ProcessId, lifetime.Token));
        }
        finally { await lifetime.CancelAsync(); await running.WaitAsync(TimeSpan.FromSeconds(2)); }
        using var restarted = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var again = new InstanceDiscoveryServer(endpoint).RunAsync(restarted.Token);
        try { Assert.Equal(endpoint, await InstanceDiscovery.ReadAsync(Environment.ProcessId, restarted.Token)); }
        finally { await restarted.CancelAsync(); await again.WaitAsync(TimeSpan.FromSeconds(2)); }
    }

    [Fact]
    public async Task StopClosesPendingAndConnectedInstances()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var server = new InstanceDiscoveryServer(ValidEndpoint());
        var running = server.RunAsync(lifetime.Token);
        await using var stalled = new NamedPipeClientStream(".", InstanceDiscovery.Name(Environment.ProcessId),
            PipeDirection.InOut, PipeOptions.Asynchronous);
        await stalled.ConnectAsync(lifetime.Token);
        await lifetime.CancelAsync();
        await running.WaitAsync(TimeSpan.FromSeconds(2));
        using var unavailable = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => InstanceDiscovery.ReadAsync(Environment.ProcessId, unavailable.Token));
    }

    [Theory]
    [InlineData("instance")]
    [InlineData("token")]
    [InlineData("path")]
    [InlineData("pid")]
    public async Task ValidatesMetadataBeforeReturningIt(string invalidField)
    {
        var endpoint = invalidField switch
        {
            "instance" => ValidEndpoint() with { InstanceId = "invalid" },
            "token" => ValidEndpoint() with { Token = "" },
            "path" => ValidEndpoint() with { ExecutablePath = "relative.exe" },
            _ => ValidEndpoint() with { ProcessId = Environment.ProcessId + 1 }
        };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var raw = new NamedPipeServerStream(InstanceDiscovery.Name(Environment.ProcessId), PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var accepted = raw.WaitForConnectionAsync(deadline.Token);
        var read = InstanceDiscovery.ReadAsync(Environment.ProcessId, deadline.Token);
        await accepted;
        await PipeProtocol.WriteAsync(raw, endpoint, deadline.Token, InstanceDiscovery.MaximumMessageBytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => read);
    }

    [Fact]
    public async Task RejectsOversizedDiscoveryFrameBeforePayloadAllocation()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var raw = new NamedPipeServerStream(InstanceDiscovery.Name(Environment.ProcessId), PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var accepted = raw.WaitForConnectionAsync(deadline.Token);
        var read = InstanceDiscovery.ReadAsync(Environment.ProcessId, deadline.Token);
        await accepted;
        await raw.WriteAsync(BitConverter.GetBytes(InstanceDiscovery.MaximumMessageBytes + 1), deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => read);
    }

    private static FlInstanceEndpoint ValidEndpoint() => new(Environment.ProcessId, Guid.NewGuid().ToString("D"),
        new string('a', 64), Environment.ProcessPath ?? throw new InvalidOperationException("Test executable path is unavailable."));
}
