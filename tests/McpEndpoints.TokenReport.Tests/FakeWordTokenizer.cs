namespace McpEndpoints.TokenReport.Tests;

/// <summary>
/// A deterministic test tokenizer that counts whitespace-separated words, so that token
/// assertions are exact and independent of the production <see cref="HeuristicTokenizer"/>.
/// </summary>
public sealed class FakeWordTokenizer : ITokenizer
{
    public int CountTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
