using System.Text.Json;
using FlMcp.Plugin;
using FlMcp.Protocol;
using Xunit;

namespace FlMcp.Tests;

/// <summary>Managed-session sample policy: workspace files pass, list_samples library entries ([P]/[U] tagged or
/// full paths under a root) are copied into &lt;workspace&gt;\staged-samples, anything else is refused.</summary>
public sealed class ScriptingSessionPolicyTests
{
    private static JsonElement Invoke(string operation, string samplePath) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { operation, arguments = new { samplePath } })).RootElement.Clone();

    private static string SamplePath(JsonElement prepared) => prepared.GetProperty("arguments").GetProperty("samplePath").GetString()!;

    private static (WorkspacePaths workspace, string library, ScriptingSessionPolicy policy) Setup(TestFiles files)
    {
        var workspace = new WorkspacePaths(files.PathFor("workspace"));
        var library = files.PathFor("library");
        Directory.CreateDirectory(Path.Combine(library, "Drums", "Kicks"));
        TestFiles.WriteWave(Path.Combine(library, "Drums", "Kicks", "Chromo Kick.wav"));
        var policy = new ScriptingSessionPolicy(workspace, [(library, "[P]")]);
        return (workspace, library, policy);
    }

    [Fact]
    public void WorkspaceRelativeSamplePassesThroughUnchanged()
    {
        using var files = new TestFiles();
        var (workspace, _, policy) = Setup(files);
        var staged = Path.Combine(workspace.Root, "song", "samples", "Kick.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        TestFiles.WriteWave(staged);

        var prepared = policy.Prepare("invoke", Invoke("add_sample_channel", "song/samples/Kick.wav"));

        Assert.Equal(staged, SamplePath(prepared), ignoreCase: true);
    }

    [Fact]
    public void TaggedLibraryEntryIsCopiedIntoTheWorkspace()
    {
        using var files = new TestFiles();
        var (workspace, library, policy) = Setup(files);

        var prepared = policy.Prepare("invoke", Invoke("add_sample_channel", @"[P]Drums\Kicks\Chromo Kick.wav"));

        var expected = Path.Combine(workspace.Root, ScriptingSessionPolicy.StagedSamplesFolder, "packs", "Drums", "Kicks", "Chromo Kick.wav");
        Assert.Equal(expected, SamplePath(prepared), ignoreCase: true);
        Assert.True(File.Exists(expected));
        Assert.Equal(new FileInfo(Path.Combine(library, "Drums", "Kicks", "Chromo Kick.wav")).Length, new FileInfo(expected).Length);

        // A second request reuses the staged copy (same path, no error) and the replace operation takes the same route.
        var again = policy.Prepare("invoke", Invoke("replace_channel_sample", @"[P]Drums\Kicks\Chromo Kick.wav"));
        Assert.Equal(expected, SamplePath(again), ignoreCase: true);
    }

    [Fact]
    public void FullPathUnderALibraryRootIsStagedToo()
    {
        using var files = new TestFiles();
        var (workspace, library, policy) = Setup(files);

        var prepared = policy.Prepare("invoke", Invoke("add_sample_channel", Path.Combine(library, "Drums", "Kicks", "Chromo Kick.wav")));

        Assert.StartsWith(Path.Combine(workspace.Root, ScriptingSessionPolicy.StagedSamplesFolder), SamplePath(prepared), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingLibraryEntryAndOutsidePathsAreRefused()
    {
        using var files = new TestFiles();
        var (_, _, policy) = Setup(files);

        var missing = Assert.Throws<FileNotFoundException>(() => policy.Prepare("invoke", Invoke("add_sample_channel", @"[P]Drums\Kicks\Nope.wav")));
        Assert.Contains("list_samples", missing.Message);

        var outside = files.PathFor("elsewhere.wav");
        TestFiles.WriteWave(outside);
        Assert.ThrowsAny<ArgumentException>(() => policy.Prepare("invoke", Invoke("add_sample_channel", outside)));
    }

    [Fact]
    public void BatchOperationsAreStagedIndividually()
    {
        using var files = new TestFiles();
        var (workspace, _, policy) = Setup(files);
        var batch = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            operations = new object[]
            {
                new { operation = "add_sample_channel", arguments = new { samplePath = @"[P]Drums\Kicks\Chromo Kick.wav" } },
                new { operation = "set_tempo", arguments = new { bpm = 120 } },
            }
        })).RootElement.Clone();

        var prepared = policy.Prepare("batch", batch);

        var first = prepared.GetProperty("operations")[0].GetProperty("arguments").GetProperty("samplePath").GetString()!;
        Assert.StartsWith(Path.Combine(workspace.Root, ScriptingSessionPolicy.StagedSamplesFolder), first, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Live finding 2026-09-18: a user's own library reaches FL only as a browser extra search folder,
    /// which the lister now tags [B1], [B2], ... Staging has to accept those entries too, each under its own
    /// subfolder, or "use my sampled drums" is refused exactly where it used to come back empty.</summary>
    [Fact]
    public void BrowserRootEntryIsStagedUnderItsOwnTagFolder()
    {
        using var files = new TestFiles();
        var workspace = new WorkspacePaths(files.PathFor("workspace"));
        var browser = files.PathFor("my-drums");
        Directory.CreateDirectory(Path.Combine(browser, "Kicks"));
        TestFiles.WriteWave(Path.Combine(browser, "Kicks", "My Kick.wav"));
        var policy = new ScriptingSessionPolicy(workspace, [(browser, "[B1]")]);

        var prepared = policy.Prepare("invoke", Invoke("add_sample_channel", @"[B1]Kicks\My Kick.wav"));

        var expected = Path.Combine(workspace.Root, ScriptingSessionPolicy.StagedSamplesFolder, "b1", "Kicks", "My Kick.wav");
        Assert.Equal(expected, SamplePath(prepared), ignoreCase: true);
        Assert.True(File.Exists(expected));

        // A full path under the same root takes the same route and reuses the copy.
        var full = policy.Prepare("invoke", Invoke("replace_channel_sample", Path.Combine(browser, "Kicks", "My Kick.wav")));
        Assert.Equal(expected, SamplePath(full), ignoreCase: true);
    }

    [Fact]
    public void DiscoveredRootsCarryTheTagsTheSdkListerEmits()
    {
        // The default roots come from the SDK's shared FlSampleRoots, so every tag staging accepts is one the
        // lister can print, and no two roots share a tag.
        var roots = ScriptingSessionPolicy.DefaultSampleRoots();

        Assert.Contains(roots, root => root.Tag == "[U]");
        Assert.Equal(roots.Select(root => root.Tag).Distinct().Count(), roots.Count);
        Assert.All(roots, root => Assert.Matches(@"^\[(P|U|B\d+)\]$", root.Tag));
    }
}
