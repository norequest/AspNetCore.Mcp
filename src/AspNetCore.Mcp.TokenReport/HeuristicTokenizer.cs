using System.Text.RegularExpressions;

namespace AspNetCore.Mcp.TokenReport;

/// <summary>
/// An OFFLINE, deterministic approximation of GPT-style BPE token counts.
/// </summary>
/// <remarks>
/// <para>
/// This is an <b>estimate</b>, not an exact tokenizer. It does not load any vocabulary,
/// perform any I/O, or contact the network, which makes it CI-safe and fully reproducible.
/// Expect roughly <c>±20%</c> versus a real BPE tokenizer (e.g. tiktoken / cl100k).
/// </para>
/// <para>
/// How it works: the input is split with a GPT-like pre-tokenizer regex into "pieces"
/// (contractions, runs of letters, runs of digits, runs of punctuation, and whitespace).
/// Each piece then contributes a token count: short pieces count as one token, while longer
/// alphanumeric runs are charged at roughly <c>ceil(length / 4)</c> tokens, mirroring the
/// observation that BPE merges average a few characters per token. The exact constant is
/// not important; what matters is that the result is deterministic and monotonic
/// (more text never yields fewer tokens).
/// </para>
/// </remarks>
public sealed class HeuristicTokenizer : ITokenizer
{
    /// <summary>Average characters per token for longer alphanumeric runs.</summary>
    private const int CharsPerToken = 4;

    /// <summary>
    /// GPT-style pre-tokenizer pattern: contractions, letter runs, number runs,
    /// punctuation runs, and whitespace runs (each optionally preceded by a space).
    /// </summary>
    private static readonly Regex PreTokenizer = new(
        @"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    /// <remarks>Returns 0 for <see langword="null"/> or empty input.</remarks>
    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var total = 0;
        foreach (Match match in PreTokenizer.Matches(text))
        {
            total += CountPiece(match.Value);
        }

        return total;
    }

    /// <summary>
    /// Charges a single pre-token piece. Whitespace-only pieces count as a single token
    /// (matching how leading spaces typically merge into the following token in BPE, while
    /// still keeping the function monotonic). Other pieces are charged by length.
    /// </summary>
    private static int CountPiece(string piece)
    {
        // Trim a single leading space (the regex attaches it to the piece) for length purposes.
        var trimmed = piece.Length > 0 && piece[0] == ' ' ? piece.AsSpan(1) : piece.AsSpan();

        if (trimmed.IsEmpty)
        {
            // Pure whitespace run: at least one token.
            return 1;
        }

        // ceil(length / CharsPerToken), at least 1.
        var tokens = (trimmed.Length + CharsPerToken - 1) / CharsPerToken;
        return tokens < 1 ? 1 : tokens;
    }
}
