namespace McpIt.TokenReport;

/// <summary>
/// Counts the number of tokens in a piece of text.
/// </summary>
/// <remarks>
/// Implementations must be deterministic and side-effect free (no I/O, no network).
/// </remarks>
public interface ITokenizer
{
    /// <summary>
    /// Returns the (estimated) number of tokens in <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to count. Never <see langword="null"/> for well-behaved callers; an empty string returns 0.</param>
    /// <returns>A non-negative token count.</returns>
    int CountTokens(string text);
}
