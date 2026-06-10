using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpIt.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class McpToolGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "McpIt.McpToolAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: AttributeMetadataName,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ModelBuilder.Build(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(models, static (spc, model) =>
        {
            if (!model.HasDescription && model.Location is { } loc)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.MissingDescription, loc.ToLocation(), model.ToolName));
            }

            if (model.Destructive && !model.AllowDestructive && model.Location is { } dloc)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DestructiveOperation, dloc.ToLocation(), model.ToolName));
            }

            // A leftover apiVersion token after substitution means no version attribute resolved.
            if (model.RouteTemplate.IndexOf("apiVersion", System.StringComparison.OrdinalIgnoreCase) >= 0
                && model.Location is { } vloc)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnresolvedApiVersion, vloc.ToLocation(), model.ToolName));
            }

            // Qualify the hint name with the namespace: the same class+action name can recur in
            // separate per-version controllers (e.g. V1.AccountController and V2.AccountController),
            // and AddSource requires a unique hint name per generator.
            var hint = string.IsNullOrEmpty(model.Namespace)
                ? $"{model.GeneratedClassName}.g.cs"
                : $"{model.Namespace}.{model.GeneratedClassName}.g.cs";
            spc.AddSource(hint, Emitter.Emit(model));
        });
    }
}
