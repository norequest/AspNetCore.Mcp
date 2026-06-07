using McpIt.Generator.Internal;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpIt.Generator.Tests;

public class LocationInfoTests
{
    [Fact]
    public void RoundTrips_filepath_and_span()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { } }", path: "Sample.cs");
        var node = tree.GetRoot().DescendantNodes().First(n => n is MethodDeclarationSyntax);
        var original = node.GetLocation();

        var info = LocationInfo.From(original)!.Value;
        var rebuilt = info.ToLocation();

        Assert.Equal(original.SourceTree!.FilePath, rebuilt.GetLineSpan().Path);
        Assert.Equal(original.SourceSpan, rebuilt.SourceSpan);
    }
}
