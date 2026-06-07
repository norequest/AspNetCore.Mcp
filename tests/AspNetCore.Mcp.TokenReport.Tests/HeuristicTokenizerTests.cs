namespace AspNetCore.Mcp.TokenReport.Tests;

public class HeuristicTokenizerTests
{
    private static readonly ITokenizer Tokenizer = new HeuristicTokenizer();

    [Fact]
    public void CountTokens_IsDeterministic()
    {
        const string text = "Gets an order by its identifier and returns the full payload.";
        var a = Tokenizer.CountTokens(text);
        var b = Tokenizer.CountTokens(text);
        Assert.Equal(a, b);
    }

    [Fact]
    public void CountTokens_EmptyOrNull_IsZero()
    {
        Assert.Equal(0, Tokenizer.CountTokens(""));
        Assert.Equal(0, Tokenizer.CountTokens(null!));
    }

    [Theory]
    [InlineData("a", "a longer piece of text with more words")]
    [InlineData("short", "short text that is strictly longer than the first one here")]
    [InlineData("{}", "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"integer\"}}}")]
    public void CountTokens_IsMonotonic_LongerNeverFewer(string shorter, string longer)
    {
        Assert.True(Tokenizer.CountTokens(longer) >= Tokenizer.CountTokens(shorter));
    }

    [Fact]
    public void CountTokens_AppendingTextNeverReducesCount()
    {
        const string baseText = "describe the tool inputs";
        var baseCount = Tokenizer.CountTokens(baseText);
        var extendedCount = Tokenizer.CountTokens(baseText + " with additional schema properties");
        Assert.True(extendedCount >= baseCount);
    }

    [Fact]
    public void CountTokens_NonEmpty_IsPositive()
    {
        Assert.True(Tokenizer.CountTokens("hello") > 0);
    }
}
