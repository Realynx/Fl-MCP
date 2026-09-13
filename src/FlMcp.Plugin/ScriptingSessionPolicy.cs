using System.Text.Json;
using System.Text.Json.Nodes;
using FlMcp.Protocol;

namespace FlMcp.Plugin;

/// <summary>MCP-owned project/path policy around the shared SDK; it does not define a DAW API catalogue.</summary>
public sealed class ScriptingSessionPolicy(WorkspacePaths paths)
{
    private static readonly HashSet<string> ManagedLifecycle = new(StringComparer.Ordinal)
    {
        "new_project", "open_project", "save_project", "save_project_as", "save_new_version"
    };

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
            var resolved = paths.Resolve(path, Path.GetExtension(path));
            if (!File.Exists(resolved)) throw new FileNotFoundException("Stage the sample inside the managed workspace.", resolved);
            arguments[field] = resolved;
        }
    }
}
