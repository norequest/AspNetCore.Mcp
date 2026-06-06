using System;

namespace McpEndpoints;

/// <summary>
/// Marks an ASP.NET Core controller action to be exposed as an MCP tool.
/// Opt-in: only annotated actions are turned into tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class McpToolAttribute : Attribute
{
    /// <summary>Optional explicit tool name. When null, the name is derived from the method name.</summary>
    public string? Name { get; set; }
}
