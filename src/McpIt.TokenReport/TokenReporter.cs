namespace McpIt.TokenReport;

/// <summary>
/// Analyzes the token cost of an MCP tool surface.
/// </summary>
public static class TokenReporter
{
    /// <summary>
    /// Computes a ranked <see cref="TokenReport"/> for <paramref name="tools"/> using
    /// <paramref name="tokenizer"/>. Each tool's name, description, and serialized input
    /// schema are counted independently; the per-tool list is sorted by total tokens
    /// descending (ties broken by name for determinism).
    /// </summary>
    public static TokenReport Analyze(IEnumerable<ToolDescriptor> tools, ITokenizer tokenizer)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(tokenizer);

        var costs = new List<ToolTokenCost>();
        var grandTotal = 0;

        foreach (var tool in tools)
        {
            var nameTokens = tokenizer.CountTokens(tool.Name);
            var descTokens = tokenizer.CountTokens(tool.Description ?? string.Empty);
            var schemaTokens = tokenizer.CountTokens(tool.InputSchemaJson);
            var total = nameTokens + descTokens + schemaTokens;

            costs.Add(new ToolTokenCost(tool.Name, nameTokens, descTokens, schemaTokens, total));
            grandTotal += total;
        }

        var sorted = costs
            .OrderByDescending(c => c.TotalTokens)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        return new TokenReport(sorted, grandTotal);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the report exceeds the given token budget.
    /// A <paramref name="budget"/> of <see langword="null"/> (no budget) is never exceeded.
    /// </summary>
    public static bool ExceedsBudget(TokenReport report, int? budget)
    {
        ArgumentNullException.ThrowIfNull(report);
        return budget is { } b && report.TotalTokens > b;
    }
}
