using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpEndpoints.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class McpToolGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "McpEndpoints.McpToolAttribute";

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
            spc.AddSource($"{model.GeneratedClassName}.g.cs", Emitter.Emit(model));
        });
    }
}
