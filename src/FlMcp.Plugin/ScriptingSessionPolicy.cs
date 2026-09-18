using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlMcp.Protocol;
using FruityLink.Core.Hosting;

namespace FlMcp.Plugin;

/// <summary>MCP-owned project/path policy around the shared SDK; it does not define a DAW API catalogue.</summary>
/// <remarks>Sample operations accept a workspace-relative file, or an entry from the SDK's <c>list_samples</c>
/// (<c>[P]Drums\Kicks\x.wav</c> = FL factory packs, <c>[U]...</c> = the user's Image-Line documents content,
/// <c>[B1]</c>/<c>[B2]</c>/... = FL's browser extra search folders, which is where a user's own library normally
/// lives; a full path under any root works too). Library entries are copied under
/// <c>&lt;workspace&gt;\staged-samples\</c> so the FLP keeps referencing files inside the workspace, one subfolder
/// per root tag (<c>packs</c>, <c>user</c>, <c>b1</c>, ...). <paramref name="sampleRoots"/> overrides the
/// discovered roots (tests). Live finding 2026-09-17: list_samples said "pass an entry verbatim" while managed
/// sessions refused it. Live finding 2026-09-18: only FL's own two roots were searched at all, so an agent asked
/// for the user's own drums found <c>[U]</c> empty; both sides now read the SDK's shared
/// <see cref="FlSampleRoots"/> so a tag can never mean two different folders.</remarks>
public sealed class ScriptingSessionPolicy(WorkspacePaths paths, IReadOnlyList<(string Root, string Tag)>? sampleRoots = null)
{
    private static readonly HashSet<string> ManagedLifecycle = new(StringComparer.Ordinal)
    {
        "new_project", "open_project", "save_project", "save_project_as", "save_new_version"
    };

    public const string StagedSamplesFolder = "staged-samples";

    private readonly IReadOnlyList<(string Root, string Tag)> sampleRoots = sampleRoots ?? DefaultSampleRoots();

    /// <summary>The same roots and tags the SDK's <c>list_samples</c> emits, from the one shared definition:
    /// [B1], [B2], ... = FL's browser extra search folders (plus anything <c>FRUITYLINK_SAMPLE_ROOTS</c> adds),
    /// [P] = FL factory packs next to the running FL64.exe, [U] = the user's Image-Line documents content.
    /// Staging has to accept every tag the lister can emit, or a verbatim entry is refused all over again.</summary>
    public static IReadOnlyList<(string Root, string Tag)> DefaultSampleRoots() => FlSampleRoots.Discover(FlInstallDirectory());

    /// <summary>The folder holding the running FL64.exe, or null when FL is not running or will not say.</summary>
    private static string? FlInstallDirectory()
    {
        try
        {
            var install = Path.GetDirectoryName(Process.GetProcessesByName("FL64").FirstOrDefault()?.MainModule?.FileName ?? string.Empty);
            return string.IsNullOrEmpty(install) ? null : install;
        }
        catch
        {
            return null;   // no FL process, or no access to its main module: the other roots are still offered
        }
    }

    public JsonElement Prepare(string method, JsonElement parameters)
    {
        if (method is not ("invoke" or "batch")) return parameters;
        var root = JsonNode.Parse(parameters.GetRawText()) as JsonObject ?? throw new ArgumentException("Expected scripting parameters object.");
        var saves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (method == "invoke") PrepareInvocation(root, saves);
        else
        {
            var operations = root["operations"] as JsonArray ?? throw new ArgumentException("Expected batch operations array.");
            foreach (var item in operations)
                PrepareInvocation(item as JsonObject ?? throw new ArgumentException("Expected batch operation object."), saves);
        }
        return JsonSerializer.SerializeToElement(root, Messages.Json);
    }

    private void PrepareInvocation(JsonObject invocation, HashSet<string> saves)
    {
        var operation = invocation["operation"]?.GetValue<string>() ?? throw new ArgumentException("Missing operation name.");
        if (ManagedLifecycle.Contains(operation))
            throw new InvalidOperationException("Use MCP project start/save/close/render tools for managed project lifecycle; Python may use save_copy to a fresh workspace path.");
        if (operation is not ("save_copy" or "add_sample_channel" or "replace_channel_sample")) return;
        var arguments = invocation["arguments"] as JsonObject ?? throw new ArgumentException("Missing operation arguments.");
        var field = operation == "save_copy" ? "path" : "samplePath";
        var path = arguments[field]?.GetValue<string>() ?? throw new ArgumentException($"Missing {field}.");
        if (operation == "save_copy")
        {
            var resolved = paths.NewFile(path, ".flp");
            if (!saves.Add(resolved)) throw new ArgumentException("Each snapshot in a batch must use a distinct new workspace path.");
            arguments[field] = resolved;
        }
        else
        {
            arguments[field] = ResolveSample(path);
        }
    }

    /// <summary>A workspace file as-is; a library entry staged (copied) into the workspace; anything else refused.
    /// Shared by the scripting operations and the <c>add_sample</c> command behind <c>fl_sample_add</c>.</summary>
    public string ResolveSample(string path)
    {
        var extension = Path.GetExtension(path);
        var staged = TryStageLibrarySample(path.Trim(), extension);
        if (staged != null) return staged;
        var resolved = paths.Resolve(path, extension);
        if (!File.Exists(resolved))
            throw new FileNotFoundException(
                "Stage the sample inside the managed workspace, or pass a [P]/[U] entry from list_samples " +
                $"(it is copied into <workspace>\\{StagedSamplesFolder}\\).", resolved);
        return resolved;
    }

    private string? TryStageLibrarySample(string path, string extension)
    {
        foreach (var (root, tag) in sampleRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            string? relative = null;
            if (path.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
                relative = path[tag.Length..].TrimStart('\\', '/');
            else if (Path.IsPathFullyQualified(path))
            {
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(path);
                if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) relative = full[rootFull.Length..];
            }
            if (string.IsNullOrEmpty(relative)) continue;
            var source = Path.GetFullPath(Path.Combine(root, relative));
            if (!File.Exists(source)) continue;
            // One staging subfolder per root tag ("b1" for [B1]), so two roots holding the same relative path
            // cannot overwrite each other inside the workspace.
            var folder = tag.Trim('[', ']') switch { "P" => "packs", "U" => "user", var other => other.ToLowerInvariant() };
            var target = paths.Resolve(Path.Combine(StagedSamplesFolder, folder, relative), extension);
            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);
            if (!targetInfo.Exists || targetInfo.Length != sourceInfo.Length || targetInfo.LastWriteTimeUtc < sourceInfo.LastWriteTimeUtc)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);
            }
            return target;
        }
        return null;
    }
}
