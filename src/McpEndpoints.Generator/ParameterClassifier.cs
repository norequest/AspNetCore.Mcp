using Microsoft.CodeAnalysis;

namespace McpEndpoints.Generator;

public static class ParameterClassifier
{
    public static ParameterModel Classify(IParameterSymbol p, string route)
    {
        // Temporary: everything is Query. Refined in the next task.
        return new ParameterModel(
            Name: p.Name,
            TypeFullyQualified: p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Source: ParameterSource.Query);
    }
}
