using System.Text.Json;
using FlMcp.Server;
using ModelContextProtocol;
using Xunit;

namespace FlMcp.Tests;

/// <summary>Live 2026-09-17, attached session: fl_plugins_list failed with the MCP SDK's generic
/// "An error occurred invoking 'fl_plugins_list'." and no detail, while the same operation worked through
/// embedded Python. The cause of the MISSING DETAIL was this converter: it only turned five exception types into
/// an McpException, so anything else escaped the tool body unconverted and the SDK swallowed its message.</summary>
public sealed class ToolErrorTests
{
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(TimeoutException))]
    public async Task TheFiveKnownTypesStillReportTheirOwnGuidanceWithNoTypePrefix(Type type)
    {
        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(
            () => throw (Exception)Activator.CreateInstance(type, "check FL's window and retry")!));

        Assert.Equal("check FL's window and retry", error.Message);
    }

    [Theory]
    [InlineData(typeof(JsonException), "JsonException")]
    [InlineData(typeof(NotSupportedException), "NotSupportedException")]
    [InlineData(typeof(InvalidCastException), "InvalidCastException")]
    [InlineData(typeof(FormatException), "FormatException")]
    public async Task EveryOtherTypeIsConvertedAndNamedInsteadOfVanishingIntoTheGenericSdkError(Type type, string name)
    {
        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(
            () => throw (Exception)Activator.CreateInstance(type, "the plugin returned a bare string")!));

        Assert.Equal($"{name}: the plugin returned a bare string", error.Message);
    }

    [Fact]
    public async Task TokensAreRedactedFromUnknownTypesToo()
    {
        var secret = new string('b', 64);

        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(
            () => throw new JsonException($"lease {secret} rejected")));

        Assert.Equal("JsonException: lease [redacted] rejected", error.Message);
        Assert.DoesNotContain(secret, error.Message);
    }

    [Fact]
    public async Task AMessagelessFailureStillSaysSomething()
    {
        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(
            () => throw new NullReferenceException("   ")));

        Assert.Equal("NullReferenceException: (the exception carried no message)", error.Message);
    }

    [Fact]
    public async Task TheRequestsOwnCancellationPropagatesUnwrapped()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToolErrors.Run<int>(
            () => throw new OperationCanceledException(cancellation.Token), cancellation.Token));
    }

    [Fact]
    public async Task ACancellationThatIsNotThisRequestsBecomesAReadableDeadlineError()
    {
        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(
            () => throw new TaskCanceledException("A task was canceled."), CancellationToken.None));

        Assert.Contains("TaskCanceledException", error.Message);
        Assert.Contains("without this request being cancelled", error.Message);
        Assert.Contains("server's own deadline", error.Message);
    }

    [Fact]
    public async Task AnAlreadyActionableMcpFailureIsNotWrappedTwice()
    {
        var error = await Assert.ThrowsAsync<McpException>(() => ToolErrors.Run<int>(
            () => throw new McpException("No FL project is connected.")));

        Assert.Equal("No FL project is connected.", error.Message);
    }
}
