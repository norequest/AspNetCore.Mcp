namespace McpIt.TokenReport.Tests;

public class TokenReporterTests
{
    private static readonly ITokenizer Tokenizer = new FakeWordTokenizer();

    [Fact]
    public void Analyze_ComputesPerToolBreakdown()
    {
        var tools = new[]
        {
            new ToolDescriptor("getOrder", "Gets an order by id", "one two three"),
        };

        var report = TokenReporter.Analyze(tools, Tokenizer);

        var tool = Assert.Single(report.Tools);
        Assert.Equal("getOrder", tool.Name);
        Assert.Equal(1, tool.NameTokens);          // "getOrder"
        Assert.Equal(5, tool.DescriptionTokens);   // Gets an order by id
        Assert.Equal(3, tool.SchemaTokens);        // one two three
        Assert.Equal(9, tool.TotalTokens);
        Assert.Equal(9, report.TotalTokens);
    }

    [Fact]
    public void Analyze_SortsByTotalDescending_WorstFirst()
    {
        var tools = new[]
        {
            new ToolDescriptor("small", "a", "x"),                       // total 3
            new ToolDescriptor("big", "one two three four", "a b c"),    // 1 + 4 + 3 = 8
            new ToolDescriptor("medium", "one two", "a b"),             // 1 + 2 + 2 = 5
        };

        var report = TokenReporter.Analyze(tools, Tokenizer);

        Assert.Equal(new[] { "big", "medium", "small" }, report.Tools.Select(t => t.Name).ToArray());
        Assert.Equal("big", report.Tools[0].Name);
        Assert.Equal(16, report.TotalTokens);
    }

    [Fact]
    public void Analyze_TreatsNullDescriptionAsZeroTokens()
    {
        var tools = new[] { new ToolDescriptor("t", null, "{}") };

        var report = TokenReporter.Analyze(tools, Tokenizer);

        Assert.Equal(0, report.Tools[0].DescriptionTokens);
    }

    [Fact]
    public void PercentOfTotal_SumsToOneHundred_AndWorstRanksFirst()
    {
        var tools = new[]
        {
            new ToolDescriptor("small", "a", "x"),
            new ToolDescriptor("big", "one two three four", "a b c"),
            new ToolDescriptor("medium", "one two", "a b"),
        };

        var report = TokenReporter.Analyze(tools, Tokenizer);

        var sum = report.Tools.Sum(report.PercentOfTotal);
        Assert.Equal(100d, sum, precision: 6);

        // Worst offender has the highest percentage and is first.
        Assert.Equal("big", report.Tools[0].Name);
        Assert.True(report.PercentOfTotal(report.Tools[0]) >= report.PercentOfTotal(report.Tools[1]));
    }

    [Fact]
    public void PercentOfTotal_ReturnsZero_WhenTotalIsZero()
    {
        var report = TokenReporter.Analyze([], Tokenizer);
        Assert.Equal(0, report.TotalTokens);
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(100, 100, false)]
    [InlineData(100, 150, false)]
    public void ExceedsBudget_ComparesTotalAgainstBudget(int total, int budget, bool expected)
    {
        var report = new TokenReport([], total);
        Assert.Equal(expected, TokenReporter.ExceedsBudget(report, budget));
    }

    [Fact]
    public void ExceedsBudget_IsFalse_WhenNoBudget()
    {
        var report = new TokenReport([], 1_000_000);
        Assert.False(TokenReporter.ExceedsBudget(report, null));
    }
}
