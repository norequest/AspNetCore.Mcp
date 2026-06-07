namespace McpIt.TokenReport.Tests;

public class ReportRendererTests
{
    private static TokenReport SampleReport()
    {
        var tools = new[]
        {
            new ToolDescriptor("getOrder", "Gets an order.", "{\"type\":\"object\"}"),
            new ToolDescriptor("listOrders", "Lists many orders with filters", "{\"type\":\"object\"}"),
        };
        return TokenReporter.Analyze(tools, new FakeWordTokenizer());
    }

    [Fact]
    public void RenderText_ContainsHeadlineToolNamesAndTotal()
    {
        var text = ReportRenderer.RenderText(SampleReport());

        Assert.Contains("This MCP server spends", text);
        Assert.Contains("getOrder", text);
        Assert.Contains("listOrders", text);
        Assert.Contains("Total:", text);
    }

    [Fact]
    public void RenderMarkdown_ContainsToolNamesTableRowsAndTotalLine()
    {
        var md = ReportRenderer.RenderMarkdown(SampleReport());

        // Tool names present.
        Assert.Contains("getOrder", md);
        Assert.Contains("listOrders", md);

        // Looks like a markdown table: header separator and data rows.
        Assert.Contains("| # | Tool | Tokens | % of total |", md);
        Assert.Contains("| ---: | --- | ---: | ---: |", md);

        // Data rows start with a pipe and include a rank.
        var rows = md.Split('\n').Where(l => l.StartsWith("| ", StringComparison.Ordinal)).ToList();
        Assert.True(rows.Count >= 4); // headline header + separator + 2 data rows

        // Bold total line.
        Assert.Contains("**Total:", md);
    }

    [Fact]
    public void RenderMarkdown_RanksWorstOffenderFirst()
    {
        var md = ReportRenderer.RenderMarkdown(SampleReport());
        var firstDataRowIndex = md.IndexOf("| 1 |", StringComparison.Ordinal);
        var listOrdersIndex = md.IndexOf("listOrders", StringComparison.Ordinal);
        var getOrderIndex = md.IndexOf("| getOrder ", StringComparison.Ordinal);

        Assert.True(firstDataRowIndex >= 0);
        // listOrders has the longer description, so it should appear before getOrder.
        Assert.True(listOrdersIndex < getOrderIndex);
    }
}
