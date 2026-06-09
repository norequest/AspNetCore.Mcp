<div align="center">

<img src="assets/icon.png" width="120" alt="McpIt" />

# McpIt

**You already have a Web API. Expose it to AI agents as MCP tools at build time: one `[McpTool]` attribute, reflection-free for read tools, zero proxy, zero hand-written server.**

[![NuGet](https://img.shields.io/nuget/v/McpIt.svg)](https://www.nuget.org/packages/McpIt)
[![Downloads](https://img.shields.io/nuget/dt/McpIt.svg)](https://www.nuget.org/packages/McpIt)
[![CI](https://github.com/norequest/McpIt/actions/workflows/ci.yml/badge.svg)](https://github.com/norequest/McpIt/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

</div>

---

McpIt is a build-time Roslyn source generator that turns your existing ASP.NET Core endpoints into [Model Context Protocol](https://modelcontextprotocol.io) tools. The official MCP C# SDK makes you hand-write `[McpServerTool]` classes for every operation you want an agent to use. McpIt generates those tool classes for you from the controller actions and minimal-API endpoints you already have: you mark an action with `[McpTool]`, and at compile time McpIt emits the MCP tool on top of the official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK. No runtime reflection for tool discovery, no internal HTTP self-call, no separate server to write and keep in sync.

---

## Install

```bash
dotnet add package McpIt
```

`McpIt` brings in the official MCP SDK transitively, so you do not need to add `ModelContextProtocol.AspNetCore` yourself. The `[McpTool]` and `[McpToolOutput]` attributes ship in the small `McpIt.Abstractions` package, which also comes in transitively.

Minimal setup in `Program.cs`:

```csharp
using McpIt;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();   // discovers the tools McpIt generated

builder.Services.AddMcpEndpoints();   // in-process invoker for the generated tools

var app = builder.Build();
app.MapControllers();
app.MapMcp("/mcp");   // MCP server at /mcp; your API stays where it is
app.Run();
```

Your REST API runs unchanged, and an MCP server is now served at `/mcp`.

---

## Before / after

A normal ASP.NET Core controller action:

```csharp
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public string GetOrder(int id) => $"order-{id}";
}
```

The same action, exposed to AI agents. Add one attribute (and a `<summary>` for the description):

```csharp
using McpIt;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    /// <summary>Gets an order by its id.</summary>
    [HttpGet("{id}")]
    [McpTool(Name = "getOrder")]
    public string GetOrder(int id) => $"order-{id}";
}
```

At compile time McpIt generates an MCP tool class for `getOrder`. It reads the action's HTTP verb, route template, parameters, and description, and builds the tool's input schema and safety hints from them. The `id` route parameter becomes a typed tool argument, and the `<summary>` becomes the tool description. Nothing else changes in your project.

To an MCP client, a `tools/list` call now returns the tool:

```json
{
  "tools": [
    {
      "name": "getOrder",
      "description": "Gets an order by its id.",
      "inputSchema": {
        "type": "object",
        "properties": { "id": { "type": "integer" } },
        "required": ["id"]
      },
      "annotations": { "readOnlyHint": true, "idempotentHint": true }
    }
  ]
}
```

When the agent calls `getOrder`, McpIt invokes your real `GetOrder` action in-process, so your routing, model binding, validation, and business logic all run exactly as they do for an HTTP caller. There is no second HTTP request and no reflection at runtime.

---

## Why McpIt

Without McpIt you write and maintain a parallel `[McpServerTool]` class for every endpoint you want an agent to reach, keeping its parameters, schema, and description in sync with the controller by hand. McpIt removes that layer: the endpoints you already have become the tools.

The only comparable library, `Api.ToMcp`, performs an internal HTTP self-call at runtime and supports controllers only. McpIt differs on four points:

1. **Direct in-process invocation.** Tool calls run your action directly, with no internal HTTP self-call.
2. **Controllers and minimal APIs.** Both endpoint styles can be exposed with `[McpTool]`.
3. **AOT-friendly, zero runtime reflection for tool discovery.** It is a source generator, so the tool code exists at build time. The runtime and generated read (GET/HEAD) tools use only reflection-free JSON, and `McpIt` is marked `IsAotCompatible` (the trim and AOT analyzers gate it on every build). Note: tools that take a request body currently serialize it with reflection-based `System.Text.Json`, and the MCP SDK's `WithToolsFromAssembly()` registration is reflection-based, so use explicit `.WithTools<...>()` registration for a fully AOT-published app.
4. **Polished and tested.** 75 tests cover generation, invocation, output shaping, and the token report.

---

## Features

- **Controllers and minimal APIs.** Mark a controller action or a minimal-API endpoint with `[McpTool]` to opt it in. Exposure is opt-in: only annotated endpoints become tools.
- **Tool names.** `[McpTool]` derives a camelCase name from the method, or set `Name` explicitly. Placed on a controller class, `[McpTool]` sets defaults (such as `NamePrefix`) for that class's annotated actions without exposing anything on its own.
- **Output shaping with `[McpToolOutput]`.** Keep responses lean. `Fields` projects the response down to the top-level JSON properties you list (per object, or per array element), and `MaxLength` truncates the result. Shaping is best-effort: malformed JSON passes through untouched.

  ```csharp
  [HttpGet("{id}")]
  [McpTool]
  [McpToolOutput(Fields = new[] { "id", "status" }, MaxLength = 500)]
  public Order GetOrder(int id) { ... }
  ```

- **Safety hints from HTTP verbs.** MCP tool annotations are derived from the verb: GET and HEAD are read-only and idempotent; POST, PUT, PATCH, and DELETE are flagged destructive (PUT and DELETE also idempotent). Exposing a destructive operation raises a build warning until you acknowledge it with `[McpTool(AllowDestructive = true)]`.
- **MCPGEN diagnostics.** Build-time warnings keep your tool surface honest: `MCPGEN001` when a tool has no description, `MCPGEN002` when a destructive operation is exposed without acknowledgement.
- **Token-cost report.** The `mcp-token-report` tool measures what your tool list costs the model and can fail a CI build over a budget (see below).

---

## Token report tool

`mcp-token-report` is an offline analyzer that shows how many tokens your `tools/list` surface spends in the model's context. AI agents load every tool's name, description, and input schema before the user asks anything, so a large tool surface is a real, recurring context cost. The tool reads a running MCP server or a saved `tools/list` JSON file, reports per-tool and total token counts, and can gate a build with `--budget`. It is fully offline and deterministic, so it is safe in CI.

```bash
dotnet tool install -g McpIt.TokenReport.Tool

mcp-token-report http://localhost:5199/mcp            # or a saved tools-list.json
mcp-token-report http://localhost:5199/mcp --markdown # Markdown table for CI artifacts
mcp-token-report http://localhost:5199/mcp --budget 2000   # exit 1 if over budget
```

Token counts use an offline heuristic tokenizer (estimates, not exact billing): ideal for comparing tools and catching bloat.

---

## Compatibility

- **Targets .NET 8, 9, and 10.** Builds with the .NET 8 SDK and newer (the source generator loads on the .NET 8/9/10 SDK build hosts).
- **Built on the official MCP SDK.** McpIt layers on `ModelContextProtocol.AspNetCore` 1.4.0. It generates the tool classes; the official SDK serves them over the MCP transport you configure (`AddMcpServer().WithHttpTransport(...)`).
- **AOT-friendly.** Generation happens at compile time. The library is `IsAotCompatible` and the read path is reflection-free; see the note above for the request-body and tool-registration caveats.

---

## Links

- NuGet: [`McpIt`](https://www.nuget.org/packages/McpIt) · [`McpIt.Abstractions`](https://www.nuget.org/packages/McpIt.Abstractions) · [`McpIt.TokenReport.Tool`](https://www.nuget.org/packages/McpIt.TokenReport.Tool)
- Repository: [github.com/norequest/McpIt](https://github.com/norequest/McpIt)
- Official MCP C# SDK: [`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol)

## License

[MIT](LICENSE). Free for personal and commercial use, no warranty. Keep the copyright and license notice.
