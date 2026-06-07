namespace McpIt.TokenReport.Tests;

public class ToolListParserTests
{
    [Fact]
    public void Parse_ExtractsNameDescriptionAndCompactSchema()
    {
        const string json = """
        {
          "tools": [
            {
              "name": "getOrder",
              "description": "Gets an order.",
              "inputSchema": {
                "type": "object",
                "properties": { "id": { "type": "integer" } },
                "required": ["id"]
              }
            }
          ]
        }
        """;

        var tools = ToolListParser.Parse(json);

        var tool = Assert.Single(tools);
        Assert.Equal("getOrder", tool.Name);
        Assert.Equal("Gets an order.", tool.Description);

        // Compact: no whitespace between tokens.
        Assert.DoesNotContain("\n", tool.InputSchemaJson);
        Assert.DoesNotContain(": ", tool.InputSchemaJson);
        Assert.Contains("\"type\":\"object\"", tool.InputSchemaJson);
        Assert.Contains("\"required\":[\"id\"]", tool.InputSchemaJson);
    }

    [Fact]
    public void Parse_HandlesMissingDescription()
    {
        const string json = """
        { "tools": [ { "name": "noDesc", "inputSchema": { "type": "object" } } ] }
        """;

        var tool = Assert.Single(ToolListParser.Parse(json));
        Assert.Equal("noDesc", tool.Name);
        Assert.Null(tool.Description);
    }

    [Fact]
    public void Parse_DefaultsMissingInputSchemaToEmptyObject()
    {
        const string json = """
        { "tools": [ { "name": "noSchema", "description": "d" } ] }
        """;

        var tool = Assert.Single(ToolListParser.Parse(json));
        Assert.Equal("{}", tool.InputSchemaJson);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenNoToolsArray()
    {
        Assert.Empty(ToolListParser.Parse("{}"));
        Assert.Empty(ToolListParser.Parse("{ \"tools\": [] }"));
    }

    [Fact]
    public void Parse_HandlesMcpResultEnvelope()
    {
        // The shape a live MCP server returns: { "result": { "tools": [...] } }
        const string json = """
        { "jsonrpc": "2.0", "id": 1, "result": { "tools": [ { "name": "getOrder", "description": "d" } ] } }
        """;

        var tool = Assert.Single(ToolListParser.Parse(json));
        Assert.Equal("getOrder", tool.Name);
        Assert.Equal("d", tool.Description);
    }

    [Fact]
    public void Parse_ParsesMultipleTools_InOrder()
    {
        const string json = """
        { "tools": [ { "name": "a" }, { "name": "b" }, { "name": "c" } ] }
        """;

        var tools = ToolListParser.Parse(json);
        Assert.Equal(new[] { "a", "b", "c" }, tools.Select(t => t.Name).ToArray());
    }
}
