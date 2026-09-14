using System.Reflection;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using Xunit;

namespace FlMcp.Tests;

public sealed class MixerBoundaryTests
{
    public static IEnumerable<object[]> AllowedTargets() => Cases(126, 500);
    public static IEnumerable<object[]> RefusedTargets() => Cases(-1, 501);

    private static IEnumerable<object[]> Cases(params int[] targets) =>
        from operation in new[] { "mixer", "route", "add_effect", "parameters", "set_parameter" }
        from target in targets
        select new object[] { operation, target };

    [Theory]
    [MemberData(nameof(AllowedTargets))]
    public async Task ExtendedMixerIndicesReachSharedSdkValidation(string operation, int track)
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, MixerControl>();
        var recorder = (MixerControl)fl;
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));

        await dispatcher.DispatchAsync(operation, Messages.Element(ArgumentsFor(operation, track)), CancellationToken.None);

        var call = recorder.Calls[0];
        Assert.Equal(ExpectedMethod(operation), call.Name);
        Assert.Equal(track, call.Args[operation == "route" ? 1 : 0]);
    }

    [Theory]
    [MemberData(nameof(RefusedTargets))]
    public async Task CurrentAndNegativeIndicesNeverReachSdk(string operation, int track)
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, MixerControl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            dispatcher.DispatchAsync(operation, Messages.Element(ArgumentsFor(operation, track)), CancellationToken.None));

        Assert.Empty(((MixerControl)fl).Calls);
    }

    private static object ArgumentsFor(string operation, int track) => operation switch
    {
        "mixer" => new MixerArgs(track, 7000, 6400),
        "route" => new ChannelRouteArgs(0, track),
        "add_effect" => new EffectArgs(track, 0, "Fruity Balance"),
        "parameters" => new ParamListArgs(track, 0, null),
        "set_parameter" => new ParamArgs(track, 0, 1, 0.5),
        _ => throw new ArgumentException(operation)
    };

    private static string ExpectedMethod(string operation) => operation switch
    {
        "mixer" => nameof(INativeFlControl.SetMixerVolumeAsync),
        "route" => nameof(INativeFlControl.SetChannelFxRouteAsync),
        "add_effect" => nameof(INativeFlControl.AddMixerEffectAsync),
        "parameters" => nameof(INativeFlControl.ListPluginParamsAsync),
        "set_parameter" => nameof(INativeFlControl.SetPluginParamAsync),
        _ => throw new ArgumentException(operation)
    };

    [Theory]
    [InlineData(-6401)]
    [InlineData(6401)]
    [InlineData(12800)]
    public async Task MixerPanOutsideSignedNativeRangeNeverReachesSdk(int pan)
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, MixerControl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            dispatcher.DispatchAsync("mixer", Messages.Element(new MixerArgs(1, 7000, pan)), CancellationToken.None));
        Assert.Empty(((MixerControl)fl).Calls);
    }

    [Fact]
    public async Task MixerPanDefaultsToCentreAndPassesSignedValuesThrough()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, MixerControl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        Assert.Equal(0, new MixerArgs(1, 7000).Pan);
        await dispatcher.DispatchAsync("mixer", Messages.Element(new MixerArgs(1, 7000, -6400)), CancellationToken.None);
        var pan = Assert.Single(((MixerControl)fl).Calls, call => call.Name == nameof(INativeFlControl.SetMixerPanAsync));
        Assert.Equal(-6400, pan.Args[1]);
    }

    [Fact]
    public async Task DeleteMarkerForwardsIndexToSharedSdk()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, MixerControl>();
        await using var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        await dispatcher.DispatchAsync("delete_marker", Messages.Element(new IndexArgs(8)), CancellationToken.None);
        var call = Assert.Single(((MixerControl)fl).Calls);
        Assert.Equal(nameof(INativeFlControl.DeleteMarkerAsync), call.Name);
        Assert.Equal(8, call.Args[0]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            dispatcher.DispatchAsync("delete_marker", Messages.Element(new IndexArgs(-1)), CancellationToken.None));
    }

    public class MixerControl : DispatchProxy
    {
        public List<(string Name, object?[] Args)> Calls { get; } = [];

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add((targetMethod!.Name, args!));
            return targetMethod.ReturnType == typeof(Task<string>) ? Task.FromResult("Parameter listing") : Task.CompletedTask;
        }
    }
}
