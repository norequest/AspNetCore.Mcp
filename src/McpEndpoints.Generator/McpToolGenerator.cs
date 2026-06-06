using Microsoft.CodeAnalysis;

namespace McpEndpoints.Generator;

// TEMPORARY STUB - replaced with the real discovery logic in the next task.
[Generator(LanguageNames.CSharp)]
public sealed class McpToolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // intentionally empty for now
    }
}
