using System.Linq;

namespace AspNetCore.Mcp;

public static class QueryStringBuilder
{
    /// <summary>Joins non-null "key=value" pairs into a query string, or returns null if none.</summary>
    public static string? Build(string?[] pairs)
    {
        var present = pairs.Where(p => p is not null).ToArray();
        return present.Length == 0 ? null : string.Join("&", present!);
    }
}
