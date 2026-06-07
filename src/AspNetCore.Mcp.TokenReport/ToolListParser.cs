using System.Text.Json;

namespace AspNetCore.Mcp.TokenReport;

/// <summary>
/// Parses an MCP <c>tools/list</c> result JSON payload into <see cref="ToolDescriptor"/>s.
/// </summary>
public static class ToolListParser
{
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Parses the <c>tools</c> array from a <c>tools/list</c> result.
    /// Reads <c>name</c> (required), <c>description</c> (optional), and re-serializes
    /// <c>inputSchema</c> to a compact JSON string (defaulting to <c>"{}"</c> when absent).
    /// </summary>
    /// <param name="toolsListJson">The JSON document containing a top-level <c>tools</c> array.</param>
    /// <returns>The parsed tool descriptors, in document order.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="toolsListJson"/> is null.</exception>
    /// <exception cref="JsonException">If the JSON is malformed.</exception>
    public static IReadOnlyList<ToolDescriptor> Parse(string toolsListJson)
    {
        ArgumentNullException.ThrowIfNull(toolsListJson);

        using var document = JsonDocument.Parse(toolsListJson);
        var root = document.RootElement;

        var result = new List<ToolDescriptor>();

        if (!TryFindToolsArray(root, out var toolsElement))
        {
            return result;
        }

        foreach (var toolElement in toolsElement.EnumerateArray())
        {
            if (toolElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = toolElement.TryGetProperty("name", out var nameElement) &&
                       nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            string? description = null;
            if (toolElement.TryGetProperty("description", out var descElement) &&
                descElement.ValueKind == JsonValueKind.String)
            {
                description = descElement.GetString();
            }

            var schemaJson = "{}";
            if (toolElement.TryGetProperty("inputSchema", out var schemaElement) &&
                schemaElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                schemaJson = JsonSerializer.Serialize(schemaElement, CompactOptions);
            }

            result.Add(new ToolDescriptor(name, description, schemaJson));
        }

        return result;
    }

    /// <summary>
    /// Locates the <c>tools</c> array, supporting both a bare <c>{ "tools": [...] }</c> document
    /// and a full MCP response <c>{ "result": { "tools": [...] } }</c>.
    /// </summary>
    private static bool TryFindToolsArray(JsonElement root, out JsonElement toolsElement)
    {
        toolsElement = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("tools", out var direct) && direct.ValueKind == JsonValueKind.Array)
        {
            toolsElement = direct;
            return true;
        }

        if (root.TryGetProperty("result", out var resultElement) &&
            resultElement.ValueKind == JsonValueKind.Object &&
            resultElement.TryGetProperty("tools", out var nested) &&
            nested.ValueKind == JsonValueKind.Array)
        {
            toolsElement = nested;
            return true;
        }

        return false;
    }
}
