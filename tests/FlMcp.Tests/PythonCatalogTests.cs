using System.Text.Json;
using FlMcp.Server;
using Xunit;

namespace FlMcp.Tests;

public sealed class PythonCatalogTests
{
    [Theory]
    [InlineData("channelOrTrack", "channel_or_track")]
    [InlineData("paramIndex", "param_index")]
    [InlineData("displayValue", "display_value")]
    [InlineData("nextOffset", "next_offset")]
    [InlineData("track", "track")]
    public void SnakeCaseMatchesGeneratedPythonNames(string wire, string python) =>
        Assert.Equal(python, PythonCatalog.SnakeCase(wire));

    [Fact]
    public void AnnotatesOperationsParametersAndResultFields()
    {
        var catalog = JsonSerializer.SerializeToElement(new
        {
            apiVersion = 1,
            operations = new object[]
            {
                new
                {
                    name = "query_plugin_parameters",
                    parameters = new object[]
                    {
                        new { name = "channelOrTrack", type = "integer", required = true, defaultValue = (object?)null },
                        new { name = "slot", type = "integer", required = false, defaultValue = -1 },
                        new { name = "filter", type = "string", required = false, defaultValue = (object?)null },
                    },
                    returnSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            items = new { type = "array", items = new { type = "object", properties = new { displayValue = new { type = "string" } } } },
                            nextOffset = new { type = "integer" },
                        },
                    },
                },
                new { name = "get_tempo", parameters = Array.Empty<object>() },
            },
        });

        var annotated = PythonCatalog.Annotate(catalog);
        var operations = annotated.GetProperty("operations");
        var query = operations[0];
        Assert.Equal("fl.ops.query_plugin_parameters(*, channel_or_track: integer, slot: integer = -1, filter: string = None)",
            query.GetProperty("pythonSignature").GetString());
        Assert.Equal("channel_or_track", query.GetProperty("parameters")[0].GetProperty("pythonName").GetString());
        Assert.Equal("channelOrTrack", query.GetProperty("parameters")[0].GetProperty("name").GetString());
        var schema = query.GetProperty("returnSchema").GetProperty("properties");
        Assert.Equal("next_offset", schema.GetProperty("nextOffset").GetProperty("pythonName").GetString());
        Assert.Equal("display_value", schema.GetProperty("items").GetProperty("items").GetProperty("properties")
            .GetProperty("displayValue").GetProperty("pythonName").GetString());
        Assert.Equal("fl.ops.get_tempo()", operations[1].GetProperty("pythonSignature").GetString());
        var conventions = annotated.GetProperty("pythonConventions");
        Assert.Contains("snake_case", conventions.GetProperty("arguments").GetString());
        Assert.Contains("primary", conventions.GetProperty("primaryRoute").GetString());
    }

    [Fact]
    public void DocumentationNamesInstalledPackageAndPrimaryRoute()
    {
        var text = PythonDocumentation.For("fruitylink_python-0.2.0-py3-none-any.whl");
        Assert.StartsWith("Installed SDK package: fruitylink_python-0.2.0-py3-none-any.whl", text);
        Assert.Contains("primary way to work", text);
        Assert.Contains("snake_case", text);
        Assert.Contains("fruitylink_serum", text);
        Assert.Contains("resultPartial:true", text);
        Assert.Contains(PythonResults.LimitVariable, text);
    }
}
