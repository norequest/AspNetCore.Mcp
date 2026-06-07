using Microsoft.CodeAnalysis;

namespace AspNetCore.Mcp.Generator;

public static class Diagnostics
{
    public static readonly DiagnosticDescriptor MissingDescription = new(
        id: "MCPGEN001",
        title: "MCP tool has no description",
        messageFormat: "The MCP tool '{0}' has no description; add an XML <summary> or [Description] so the model knows when to call it",
        category: "AspNetCore.Mcp",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DestructiveOperation = new(
        id: "MCPGEN002",
        title: "Destructive operation exposed as MCP tool",
        messageFormat: "destructive operation '{0}' is exposed as an MCP tool; set [McpTool(AllowDestructive = true)] to acknowledge",
        category: "AspNetCore.Mcp",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
