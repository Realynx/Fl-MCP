using FlMcp.Protocol;
using FlMcp.Server;
using Xunit;

namespace FlMcp.Tests;

public sealed class WorkspaceTests
{
    [Theory]
    [InlineData("../outside.flp")]
    [InlineData("file.flp:payload")]
    [InlineData("file.flp.")]
    [InlineData("CON.flp")]
    [InlineData("sub /file.flp")]
    [InlineData("LPT1.flp")]
    public void RejectsTraversalAndWindowsAliases(string path)
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        Assert.Throws<ArgumentException>(() => workspace.Resolve(path, ".flp"));
    }

    [Fact]
    public void CreatesNestedOutputAndRefusesOverwrite()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        var path = workspace.NewFile("song/mix.flp", ".flp");
        File.WriteAllText(path, "existing");
        Assert.Throws<IOException>(() => workspace.NewFile("song/mix.flp", ".flp"));
        Assert.Equal("existing", File.ReadAllText(path));
    }

    [Fact]
    public void RejectsSiblingWithMatchingPrefix()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.Root);
        Assert.Throws<ArgumentException>(() => workspace.Resolve(files.Root + "-outside/test.flp", ".flp"));
    }

    [Fact]
    public void VerifiesProjectAndWaveContainers()
    {
        using var files = new TestFiles();
        Assert.Equal(24, Artifacts.VerifyProject(files.Project()));
        var wave = files.PathFor("mix.wav");
        TestFiles.WriteWave(wave);
        Assert.Equal(48, Artifacts.VerifyWave(wave));
        File.WriteAllText(wave, "not a wave file but enough bytes to avoid a short read");
        Assert.Throws<InvalidDataException>(() => Artifacts.VerifyWave(wave));
    }

    [Fact]
    public void RejectsWaveWithMissingAudio()
    {
        using var files = new TestFiles();
        var wave = files.PathFor("mix.wav");
        TestFiles.WriteWave(wave);
        var bytes = File.ReadAllBytes(wave);
        bytes[36] = (byte)'j';
        File.WriteAllBytes(wave, bytes);
        Assert.Throws<InvalidDataException>(() => Artifacts.VerifyWave(wave));
    }

    [Fact]
    public void RejectsTruncatedWaveChunk()
    {
        using var files = new TestFiles();
        var wave = files.PathFor("mix.wav");
        TestFiles.WriteWave(wave);
        var bytes = File.ReadAllBytes(wave);
        bytes[40] = 200;
        File.WriteAllBytes(wave, bytes);
        Assert.Throws<InvalidDataException>(() => Artifacts.VerifyWave(wave));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(22)]
    [InlineData(23)]
    public void RejectsHeaderOnlyOrTruncatedProject(int length)
    {
        using var files = new TestFiles();
        var path = files.Project();
        File.WriteAllBytes(path, File.ReadAllBytes(path)[..length]);
        Assert.Throws<InvalidDataException>(() => Artifacts.VerifyProject(path));
    }
}
