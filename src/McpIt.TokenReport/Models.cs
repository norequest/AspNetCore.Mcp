namespace McpIt.TokenReport;

/// <summary>
/// A single MCP tool as advertised by a <c>tools/list</c> result.
/// </summary>
/// <param name="Name">The tool name (the model pays tokens for this).</param>
/// <param name="Description">The optional human/LLM-facing description.</param>
/// <param name="InputSchemaJson">The tool's JSON Schema, serialized as a compact JSON string.</param>
public sealed record ToolDescriptor(string Name, string? Description, string InputSchemaJson);

/// <summary>
/// The per-tool token cost breakdown produced by <see cref="TokenReporter"/>.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="NameTokens">Tokens spent on the tool name.</param>
/// <param name="DescriptionTokens">Tokens spent on the description.</param>
/// <param name="SchemaTokens">Tokens spent on the serialized input schema.</param>
/// <param name="TotalTokens">Sum of name, description, and schema tokens.</param>
public sealed record ToolTokenCost(
    string Name,
    int NameTokens,
    int DescriptionTokens,
    int SchemaTokens,
    int TotalTokens);

/// <summary>
/// A ranked token-cost report for an MCP tool surface.
/// </summary>
/// <remarks>The <see cref="Tools"/> list is sorted by <see cref="ToolTokenCost.TotalTokens"/> descending.</remarks>
public sealed record TokenReport(IReadOnlyList<ToolTokenCost> Tools, int TotalTokens)
{
    /// <summary>
    /// Returns the percentage (0-100) of the grand total contributed by <paramref name="tool"/>.
    /// Returns 0 when the report total is 0.
    /// </summary>
    public double PercentOfTotal(ToolTokenCost tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return TotalTokens == 0 ? 0d : 100d * tool.TotalTokens / TotalTokens;
    }
}
