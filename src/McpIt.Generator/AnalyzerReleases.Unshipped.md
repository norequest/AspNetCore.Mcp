; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MCPGEN001 | McpIt | Warning | MCP tool has no description; add an XML <summary> or [Description].
MCPGEN002 | McpIt | Warning | Destructive operation exposed as an MCP tool without [McpTool(AllowDestructive = true)].
