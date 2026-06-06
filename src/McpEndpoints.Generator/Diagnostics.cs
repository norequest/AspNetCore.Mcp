using Microsoft.CodeAnalysis;

namespace McpEndpoints.Generator;

public static class Diagnostics
{
    public static readonly DiagnosticDescriptor MissingDescription = new(
        id: "MCPGEN001",
        title: "MCP tool has no description",
        messageFormat: "The MCP tool '{0}' has no description; add an XML <summary> or [Description] so the model knows when to call it",
        category: "McpEndpoints",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
