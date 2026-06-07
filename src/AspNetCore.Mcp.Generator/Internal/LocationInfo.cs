using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AspNetCore.Mcp.Generator.Internal;

public readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? From(Location location)
    {
        if (location.SourceTree is null) return null;
        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
    }
}
