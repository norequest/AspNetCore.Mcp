using System.Linq;
using McpEndpoints.Generator.Internal;
using Microsoft.CodeAnalysis;

namespace McpEndpoints.Generator;

public static class ModelBuilder
{
    public static EndpointModel? Build(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;

        var ns = method.ContainingType.ContainingNamespace.IsGlobalNamespace
            ? "Generated"
            : method.ContainingType.ContainingNamespace.ToDisplayString();

        var className = $"{method.ContainingType.Name}_{method.Name}_Tool";
        var toolName = method.Name; // refined to camelCase + explicit name in a later task

        return new EndpointModel(
            Namespace: ns,
            GeneratedClassName: className,
            ToolName: toolName,
            Description: null,
            HttpMethod: "GET",
            RouteTemplate: string.Empty,
            Parameters: new EquatableArray<ParameterModel>(Enumerable.Empty<ParameterModel>()),
            Location: LocationInfo.From(method.Locations.FirstOrDefault() ?? Location.None));
    }
}
