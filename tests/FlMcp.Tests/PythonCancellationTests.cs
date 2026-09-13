using System.Reflection;
using System.IO.Pipes;
using System.Text.Json;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FlMcp.Server;
using FruityLink.Core.Abstractions;
using FruityLink.Scripting;
using Xunit;

namespace FlMcp.Tests;

public sealed class PythonCancellationTests
{
    [Fact]
    public async Task EmbeddedCancellationAcknowledgesOnlyAfterDirectNativeCallbackDrains()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, BlockingControl>();
        var control = (BlockingControl)fl;
        control.Project = files.Project();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root), handler => new DirectRuntime(handler));
        using var serverLifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = new BridgeServer(PipeProtocol.Name(Environment.ProcessId), "token", dispatcher.DispatchAsync, _ => { });
        var serving = server.RunAsync(serverLifetime.Token);
        using var cancellation = new CancellationTokenSource();
        var request = new BridgeClient().CallAsync(Environment.ProcessId, "token", "python_execute",
            new PythonExecute("unused by fake", 200, control.Project), 200, cancellation.Token);
        try
        {
            await control.Started.Task.WaitAsync(serverLifetime.Token);
            cancellation.Cancel();
            await Task.Delay(100, serverLifetime.Token);
            Assert.False(request.IsCompleted);
            control.Release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            Assert.Equal(new[] { "SetTempoAsync" }, control.Calls);
        }
        finally
        {
            control.Release.TrySetResult();
            serverLifetime.Cancel();
            await serving;
        }
    }

    [Fact]
    public async Task LostExecutionPipeIsReportedAsUnknownCompletion()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pipe = new NamedPipeServerStream(PipeProtocol.Name(Environment.ProcessId), PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var client = new BridgeClient().CallAsync(Environment.ProcessId, "token", "python_execute",
            new PythonExecute("result = 1", 10, @"C:\fixture.flp"), 10, deadline.Token);
        await pipe.WaitForConnectionAsync(deadline.Token);
        var request = await PipeProtocol.ReadAsync<BridgeRequest>(pipe, deadline.Token);
        Assert.Equal("python_execute", request.Operation);
        await pipe.DisposeAsync();
        await Assert.ThrowsAsync<BridgeCompletionUnknownException>(() => client);
    }

    [Fact]
    public async Task BridgeCancellationWaitsForNativeDrainAndStopsLaterBatchItems()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, BlockingControl>();
        var control = (BlockingControl)fl;
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        using var serverLifetime = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = new BridgeServer(PipeProtocol.Name(Environment.ProcessId), "token", dispatcher.DispatchAsync, _ => { });
        var serving = server.RunAsync(serverLifetime.Token);
        using var requestCancellation = new CancellationTokenSource();
        var parameters = Messages.Element(new { operations = new object[] {
            new { operation = "set_tempo", arguments = new { bpm = 123 } },
            new { operation = "set_master_pitch", arguments = new { cents = 1 } }
        } });
        var request = new BridgeClient().CallAsync(Environment.ProcessId, "token", "python_call", new PythonCall("batch", parameters), 10, requestCancellation.Token);
        try
        {
            await control.Started.Task.WaitAsync(serverLifetime.Token);
            requestCancellation.Cancel();
            await Task.Delay(100, serverLifetime.Token);
            Assert.False(request.IsCompleted);
            control.Release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            Assert.Equal(new[] { "SetTempoAsync" }, control.Calls);
        }
        finally
        {
            control.Release.TrySetResult();
            serverLifetime.Cancel();
            await serving;
        }
    }

    public class BlockingControl : DispatchProxy
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Calls { get; } = [];
        public string Project { get; set; } = "";
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == "GetProjectInfoAsync") return Task.FromResult("Path: " + Project + "\nSaved: yes");
            Calls.Add(targetMethod!.Name);
            Started.TrySetResult();
            return Release.Task;
        }
    }

    private sealed class DirectRuntime(Func<string, JsonElement, CancellationToken, Task<object?>> handler) : IEmbeddedPythonRuntime
    {
        public async Task<JsonElement> ExecuteAsync(string code, int timeoutSeconds, CancellationToken ct = default)
        {
            var result = await handler("batch", Messages.Element(new { operations = new object[] {
                new { operation = "set_tempo", arguments = new { bpm = 123 } },
                new { operation = "set_master_pitch", arguments = new { cents = 1 } }
            } }), ct);
            return Messages.Element(new { ok = true, result });
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
