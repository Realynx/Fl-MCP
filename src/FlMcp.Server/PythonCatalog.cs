using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlMcp.Server;

/// <summary>Annotates the shared SDK catalog with the exact spellings the embedded fruitylink package expects.
/// The wire contract stays camelCase; the annotations tell a script author which Python names to type.</summary>
public static class PythonCatalog
{
    public static JsonElement Annotate(JsonElement catalog)
    {
        if (JsonNode.Parse(catalog.GetRawText()) is not JsonObject root) return catalog;
        root["pythonConventions"] = Conventions();
        if (root["operations"] is JsonArray operations)
            foreach (var operation in operations.OfType<JsonObject>()) AnnotateOperation(operation);
        return JsonSerializer.SerializeToElement(root);
    }

    /// <summary>camelCase or PascalCase wire name to the snake_case name fruitylink generates.</summary>
    public static string SnakeCase(string name)
    {
        var text = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) text.Append('_');
            text.Append(char.ToLowerInvariant(c));
        }
        return text.ToString();
    }

    private static JsonObject Conventions() => new()
    {
        ["primaryRoute"] = "fl_execute_python is the primary way to work: one request can run many operations through fl.channels, fl.patterns, fl.clips, fl.playlist, fl.mixer, fl.automation, fl.transport, fl.plugins, fl.analysis and fl.ops. The single-operation MCP tools are conveniences, not the only route.",
        ["arguments"] = "Python arguments are keyword-only snake_case: channelOrTrack -> channel_or_track, paramIndex -> param_index. Each parameter below carries pythonName; each operation carries pythonSignature.",
        ["typedResults"] = "query_* operations return dataclasses whose attributes are snake_case (displayValue -> display_value, nextOffset -> next_offset); returnSchema properties carry pythonName. fl.ops.invoke(...) and other raw dict results keep the camelCase wire keys.",
        ["result"] = "Assign a JSON-compatible value to result; dict keys must be strings. A raised exception discards the whole result dict, so wrap steps in try/except and record errors in the result. Write oversized dumps to a file from inside FL and return a summary.",
    };

    private static void AnnotateOperation(JsonObject operation)
    {
        var parameters = operation["parameters"] as JsonArray;
        var pieces = new List<string>();
        if (parameters is not null)
            foreach (var parameter in parameters.OfType<JsonObject>())
            {
                var wire = parameter["name"]?.GetValue<string>() ?? "";
                var python = SnakeCase(wire);
                parameter["pythonName"] = python;
                pieces.Add(Signature(parameter, python));
            }
        var name = operation["name"]?.GetValue<string>() ?? "";
        operation["pythonSignature"] = pieces.Count == 0 ? $"fl.ops.{name}()" : $"fl.ops.{name}(*, {string.Join(", ", pieces)})";
        if (operation["returnSchema"] is JsonObject schema) AnnotateSchema(schema);
    }

    private static string Signature(JsonObject parameter, string python)
    {
        var type = parameter["type"]?.GetValue<string>() ?? "any";
        var required = parameter["required"]?.GetValue<bool>() ?? true;
        if (required) return $"{python}: {type}";
        return $"{python}: {type} = {PythonLiteral(parameter["defaultValue"])}";
    }

    private static string PythonLiteral(JsonNode? value) => value switch
    {
        null => "None",
        JsonValue scalar when scalar.TryGetValue<bool>(out var flag) => flag ? "True" : "False",
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => JsonSerializer.Serialize(text),
        _ => value.ToJsonString(),
    };

    private static void AnnotateSchema(JsonObject schema)
    {
        if (schema["properties"] is JsonObject properties)
            foreach (var property in properties.ToList())
            {
                if (property.Value is not JsonObject propertySchema) continue;
                propertySchema["pythonName"] = SnakeCase(property.Key);
                AnnotateSchema(propertySchema);
            }
        if (schema["items"] is JsonObject items) AnnotateSchema(items);
    }
}
