using System;

namespace AspNetCore.Mcp;

/// <summary>
/// Marks an ASP.NET Core controller action to be exposed as an MCP tool.
/// Opt-in: only annotated actions are turned into tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>Optional explicit tool name. When null, the name is derived from the method name.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Acknowledges that this tool performs a destructive (state-changing) operation.
    /// Set to <c>true</c> to suppress the MCPGEN002 destructive-operation warning.
    /// </summary>
    public bool AllowDestructive { get; set; }
}
