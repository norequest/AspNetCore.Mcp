using System.Linq;
using System.Xml.Linq;
using AspNetCore.Mcp.Generator.Internal;
using Microsoft.CodeAnalysis;

namespace AspNetCore.Mcp.Generator;

public static class ModelBuilder
{
    private static readonly (string Attr, string Verb)[] VerbAttributes =
    {
        ("Microsoft.AspNetCore.Mvc.HttpGetAttribute", "GET"),
        ("Microsoft.AspNetCore.Mvc.HttpPostAttribute", "POST"),
        ("Microsoft.AspNetCore.Mvc.HttpPutAttribute", "PUT"),
        ("Microsoft.AspNetCore.Mvc.HttpDeleteAttribute", "DELETE"),
        ("Microsoft.AspNetCore.Mvc.HttpPatchAttribute", "PATCH"),
    };

    public static EndpointModel? Build(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method) return null;

        var ns = method.ContainingType.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : method.ContainingType.ContainingNamespace.ToDisplayString();

        var className = $"{method.ContainingType.Name}_{method.Name}_Tool";

        var mcpAttr = ctx.Attributes.FirstOrDefault();
        var explicitName = mcpAttr?.NamedArguments
            .FirstOrDefault(kv => kv.Key == "Name").Value.Value as string;
        var toolName = string.IsNullOrWhiteSpace(explicitName)
            ? ToCamelCase(method.Name)
            : explicitName!;

        var allowDestructive = mcpAttr?.NamedArguments
            .FirstOrDefault(kv => kv.Key == "AllowDestructive").Value.Value is bool b && b;

        var (httpMethod, methodRoute) = GetVerbAndRoute(method);
        var classRoute = GetClassRoute(method.ContainingType);
        var route = CombineRoutes(classRoute, methodRoute);

        var description = GetXmlSummary(method) ?? GetDescriptionAttribute(method);

        var (readOnly, destructive, idempotent) = DeriveSafety(httpMethod);

        var (outputMaxLength, outputFields) = GetOutputShaping(method);

        var parameters = method.Parameters
            .Select(p => ParameterClassifier.Classify(p, route))
            .ToArray();

        return new EndpointModel(
            Namespace: ns,
            GeneratedClassName: className,
            ToolName: toolName,
            Description: description,
            HttpMethod: httpMethod,
            RouteTemplate: route,
            Parameters: new EquatableArray<ParameterModel>(parameters),
            ReadOnly: readOnly,
            Destructive: destructive,
            Idempotent: idempotent,
            AllowDestructive: allowDestructive,
            OutputMaxLength: outputMaxLength,
            OutputFields: new EquatableArray<string>(outputFields),
            Location: LocationInfo.From(method.Locations.FirstOrDefault() ?? Location.None));
    }

    private static (bool ReadOnly, bool Destructive, bool Idempotent) DeriveSafety(string verb) => verb switch
    {
        "GET" => (true, false, true),
        "HEAD" => (true, false, true),
        "POST" => (false, true, false),
        "PUT" => (false, true, true),
        "PATCH" => (false, true, false),
        "DELETE" => (false, true, true),
        _ => (false, false, false),
    };

    private static (int? MaxLength, string[] Fields) GetOutputShaping(IMethodSymbol method)
    {
        var attr = method.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "AspNetCore.Mcp.McpToolOutputAttribute");
        if (attr is null)
            return (null, System.Array.Empty<string>());

        int? maxLength = null;
        var maxArg = attr.NamedArguments.FirstOrDefault(kv => kv.Key == "MaxLength");
        if (maxArg.Key == "MaxLength" && maxArg.Value.Value is int m && m > 0)
            maxLength = m;

        var fields = System.Array.Empty<string>();
        var fieldsArg = attr.NamedArguments.FirstOrDefault(kv => kv.Key == "Fields");
        if (fieldsArg.Key == "Fields" && !fieldsArg.Value.IsNull)
        {
            fields = fieldsArg.Value.Values
                .Select(v => v.Value as string)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToArray();
        }

        return (maxLength, fields);
    }

    private static (string Verb, string Route) GetVerbAndRoute(IMethodSymbol method)
    {
        foreach (var attr in method.GetAttributes())
        {
            var name = attr.AttributeClass?.ToDisplayString();
            var match = VerbAttributes.FirstOrDefault(v => v.Attr == name);
            if (match.Attr is not null)
            {
                var route = attr.ConstructorArguments.Length > 0
                    ? attr.ConstructorArguments[0].Value as string ?? string.Empty
                    : string.Empty;
                return (match.Verb, route);
            }
        }
        return ("GET", string.Empty);
    }

    private static string GetClassRoute(INamedTypeSymbol type)
    {
        var routeAttr = type.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.RouteAttribute");
        if (routeAttr is { ConstructorArguments.Length: > 0 })
            return routeAttr.ConstructorArguments[0].Value as string ?? string.Empty;
        return string.Empty;
    }

    private static string CombineRoutes(string prefix, string suffix)
    {
        prefix = prefix.Trim('/');
        suffix = suffix.Trim('/');
        if (prefix.Length == 0) return suffix;
        if (suffix.Length == 0) return prefix;
        return $"{prefix}/{suffix}";
    }

    private static string? GetXmlSummary(IMethodSymbol method)
    {
        var xml = method.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            var summary = XDocument.Parse(xml).Descendants("summary").FirstOrDefault();
            var text = summary?.Value.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDescriptionAttribute(IMethodSymbol method)
    {
        var attr = method.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DescriptionAttribute");
        if (attr is { ConstructorArguments.Length: > 0 })
            return attr.ConstructorArguments[0].Value as string;
        return null;
    }

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
}
