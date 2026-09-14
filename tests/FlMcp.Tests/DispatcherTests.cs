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

    [Fact]
    public void ReportsFileNameWhenFlLeavesAManagedProjectUntitled()
    {
        var status = new SessionStatus(true, 1, "Title: (untitled)\nPath: C:/work/session-v006-lead.flp\nSaved: yes", 140, 96)
            { ProjectPath = @"C:\work\session-v006-lead.flp", ProjectTitle = "" };
        var shown = CommandDispatcher.WithDisplayTitle(status);
        Assert.Equal("session-v006-lead", shown.ProjectTitle);
        Assert.StartsWith("Title: session-v006-lead (file name)\nPath: ", shown.Project);
    }

    [Fact]
    public void KeepsRealTitlesAndTrulyUntitledProjects()
    {
        var titled = new SessionStatus(true, 1, "Title: Song\nPath: C:/work/song.flp\nSaved: yes", 140, 96) { ProjectTitle = "Song", ProjectPath = @"C:\work\song.flp" };
        Assert.Same(titled, CommandDispatcher.WithDisplayTitle(titled));
        var untitled = new SessionStatus(true, 1, "Title: (untitled)\nPath: (none)\nSaved: no", 140, 96) { ProjectTitle = "", Untitled = true };
        Assert.Same(untitled, CommandDispatcher.WithDisplayTitle(untitled));
    }
}
