using System.Reflection;
using FlMcp.Plugin;
using FlMcp.Protocol;
using FruityLink.Core.Abstractions;
using Xunit;

namespace FlMcp.Tests;

public sealed class DispatcherTests
{
    [Fact]
    public async Task BatchValidationCompletesBeforeNativeMutation()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, RecordingFl>();
        var recorder = (RecordingFl)fl;
        var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        Note[] notes = [new(0, 60, 0, 96, 100), new(0, 60, int.MaxValue - 1, 96, 100)];
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => dispatcher.DispatchAsync("add_notes", Messages.Element(new NotesArgs(1, notes)), CancellationToken.None));
        Assert.Empty(recorder.Calls);
    }

    [Fact]
    public async Task ConvertsNotesToTypedPublicSdkContract()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, RecordingFl>();
        var recorder = (RecordingFl)fl;
        var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        await dispatcher.DispatchAsync("add_notes", Messages.Element(new NotesArgs(2, [new(3, 60, 96, 48, 100)])), CancellationToken.None);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal("AddNotesAsync", call.Name);
        Assert.Equal(2, call.Args[0]);
        Assert.Equal(new NoteSpec(3, 60, 96, 48, 100), Assert.Single((IReadOnlyList<NoteSpec>)call.Args[1]!));
    }

    [Fact]
    public async Task UnknownOperationsCannotReachNativeInterface()
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, RecordingFl>();
        var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        await Assert.ThrowsAsync<ArgumentException>(() => dispatcher.DispatchAsync("OpenProjectAsync", Messages.Element(new { }), CancellationToken.None));
        Assert.Empty(((RecordingFl)fl).Calls);
    }

    public class RecordingFl : DispatchProxy
    {
        public List<(string Name, object?[] Args)> Calls { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add((targetMethod!.Name, args!));
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task RejectsPlaylistTrackOutsideOneBasedRange(int track)
    {
        using var files = new TestFiles();
        var fl = DispatchProxy.Create<INativeFlControl, RecordingFl>();
        var dispatcher = new CommandDispatcher(fl, new WorkspacePaths(files.Root));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => dispatcher.DispatchAsync("add_clip", Messages.Element(new ClipArgs(1, track, 0, 96)), CancellationToken.None));
        Assert.Empty(((RecordingFl)fl).Calls);
    }
}
