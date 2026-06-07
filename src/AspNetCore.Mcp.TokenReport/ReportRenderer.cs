using System.Globalization;
using System.Text;

namespace AspNetCore.Mcp.TokenReport;

/// <summary>
/// Renders a <see cref="TokenReport"/> as human-readable text or Markdown.
/// </summary>
public static class ReportRenderer
{
    /// <summary>
    /// Renders a screenshot-friendly plain-text report: a headline, a ranked aligned table
    /// (rank, tool, total tokens, % of total), and a total line.
    /// </summary>
    public static string RenderText(TokenReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.Append("This MCP server spends ")
            .Append(report.TotalTokens.ToString(CultureInfo.InvariantCulture))
            .Append(" tokens listing ")
            .Append(report.Tools.Count.ToString(CultureInfo.InvariantCulture))
            .Append(report.Tools.Count == 1 ? " tool." : " tools.")
            .Append('\n')
            .Append('\n');

        // Column widths.
        var nameWidth = "Tool".Length;
        foreach (var tool in report.Tools)
        {
            nameWidth = Math.Max(nameWidth, tool.Name.Length);
        }

        const string rankHeader = "#";
        const string totalHeader = "Tokens";
        const string pctHeader = "% of total";

        var rankWidth = Math.Max(rankHeader.Length, report.Tools.Count.ToString(CultureInfo.InvariantCulture).Length);

        sb.Append(rankHeader.PadLeft(rankWidth)).Append("  ")
            .Append("Tool".PadRight(nameWidth)).Append("  ")
            .Append(totalHeader.PadLeft(8)).Append("  ")
            .Append(pctHeader.PadLeft(10)).Append('\n');

        sb.Append(new string('-', rankWidth)).Append("  ")
            .Append(new string('-', nameWidth)).Append("  ")
            .Append(new string('-', 8)).Append("  ")
            .Append(new string('-', 10)).Append('\n');

        var rank = 1;
        foreach (var tool in report.Tools)
        {
            sb.Append(rank.ToString(CultureInfo.InvariantCulture).PadLeft(rankWidth)).Append("  ")
                .Append(tool.Name.PadRight(nameWidth)).Append("  ")
                .Append(tool.TotalTokens.ToString(CultureInfo.InvariantCulture).PadLeft(8)).Append("  ")
                .Append(FormatPercent(report.PercentOfTotal(tool)).PadLeft(10)).Append('\n');
            rank++;
        }

        sb.Append('\n');
        sb.Append("Total: ")
            .Append(report.TotalTokens.ToString(CultureInfo.InvariantCulture))
            .Append(" tokens (estimated, offline heuristic).");

        return sb.ToString();
    }

    /// <summary>
    /// Renders the report as a Markdown document: a headline, a ranked Markdown table
    /// (rank, tool, total tokens, % of total), and a bold total line.
    /// </summary>
    public static string RenderMarkdown(TokenReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        sb.Append("# MCP token report\n\n");
        sb.Append("This MCP server spends **")
            .Append(report.TotalTokens.ToString(CultureInfo.InvariantCulture))
            .Append("** tokens listing **")
            .Append(report.Tools.Count.ToString(CultureInfo.InvariantCulture))
            .Append("** ")
            .Append(report.Tools.Count == 1 ? "tool" : "tools")
            .Append(".\n\n");

        sb.Append("| # | Tool | Tokens | % of total |\n");
        sb.Append("| ---: | --- | ---: | ---: |\n");

        var rank = 1;
        foreach (var tool in report.Tools)
        {
            sb.Append("| ")
                .Append(rank.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(EscapeCell(tool.Name)).Append(" | ")
                .Append(tool.TotalTokens.ToString(CultureInfo.InvariantCulture)).Append(" | ")
                .Append(FormatPercent(report.PercentOfTotal(tool))).Append(" |\n");
            rank++;
        }

        sb.Append("\n**Total: ")
            .Append(report.TotalTokens.ToString(CultureInfo.InvariantCulture))
            .Append(" tokens** (estimated, offline heuristic).\n");

        return sb.ToString();
    }

    private static string FormatPercent(double percent) =>
        percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string EscapeCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);
}
