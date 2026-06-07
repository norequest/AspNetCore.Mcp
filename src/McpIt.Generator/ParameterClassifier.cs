using System.Linq;
using Microsoft.CodeAnalysis;

namespace McpIt.Generator;

public static class ParameterClassifier
{
    // Fully-qualified format WITHOUT special-type keywords (so int -> global::System.Int32),
    // but keeping nullable reference type annotations (so string? stays string?).
    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            (SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes)
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static ParameterModel Classify(IParameterSymbol p, string route)
    {
        var typeName = p.Type.ToDisplayString(TypeFormat);
        var source = DetermineSource(p, route);
        return new ParameterModel(p.Name, typeName, source);
    }

    private static ParameterSource DetermineSource(IParameterSymbol p, string route)
    {
        if (route.Contains("{" + p.Name + "}"))
            return ParameterSource.Route;

        var hasFromBody = p.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "Microsoft.AspNetCore.Mvc.FromBodyAttribute");
        if (hasFromBody) return ParameterSource.Body;

        if (IsComplex(p.Type)) return ParameterSource.Body;

        return ParameterSource.Query;
    }

    private static bool IsComplex(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None) return false; // string, int, bool, etc.
        if (type.TypeKind == TypeKind.Enum) return false;
        if (type is INamedTypeSymbol { IsGenericType: true } g &&
            g.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            return false; // Nullable<primitive>
        return type.TypeKind is TypeKind.Class or TypeKind.Struct;
    }
}
