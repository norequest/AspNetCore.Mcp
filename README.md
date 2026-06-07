# McpEndpoints

Turn your ASP.NET Core API into **token-efficient** MCP tools at build time.

McpEndpoints is a .NET source generator that sits **on top of** the official
[`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) SDK.
The official SDK gives you an MCP endpoint. McpEndpoints gives you a **curated, lean,
and safe** one, generated from the endpoints you already have, and tells you what it
costs in tokens.

Everything runs at compile time and is fully offline and deterministic. No network
calls, no AI calls, no per-build cost. CI-safe.

> Status: Phase 1-4 complete, 65 tests green. Pre-release; APIs may change.

## Why

Expose hundreds of API endpoints as MCP tools naively and the model's tool list
balloons, eating the context window before the user asks anything. McpEndpoints
makes tool exposure **opt-in**, generates **editable** tool code you can review in a
PR, shows you the **token cost**, lets you **trim tool output**, and **flags
destructive operations**.

## Quick start

1. Reference the packages (project references for now):

   - `McpEndpoints.Abstractions` — the `[McpTool]` attribute
   - `McpEndpoints.Generator` — the source generator (as an analyzer)
   - `McpEndpoints` — the runtime invoker + DI

2. Mark the actions you want exposed and register the runtime:

   ```csharp
   // Program.cs
   builder.Services.AddControllers();
   builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();
   builder.Services.AddMcpEndpoints(o => o.BaseAddress = new Uri("https://localhost:5001/"));

   var app = builder.Build();
   app.MapControllers();
   app.MapMcp();
   app.Run();
   ```

   ```csharp
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

3. Build. The generator emits an editable `[McpServerToolType]` class per marked
   action; the official SDK discovers it via `WithToolsFromAssembly()`. When the tool
   is called, it loops back to your real endpoint, reusing ASP.NET routing, model
   binding, filters, and validation.

   > To use XML `<summary>` as the tool description, enable
   > `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in your project.
   > `[Description]` works without it.

## Features

### Build-time tool generation (Phase 1)
Controller actions marked `[McpTool]` become MCP tools. Opt-in only. Parameters are
classified into route / query / body. A `MCPGEN001` warning fires when a tool has no
description (the #1 cause of poor tool calling).

### Token-cost report (Phase 2)
See what your tool surface costs the model.

```
$ mcp-token-report tools-list.json
This MCP server spends 143 tokens listing 3 tools.

#  Tool            Tokens  % of total
-  ------------  --------  ----------
1  searchOrders       103       72.0%
2  getOrder            33       23.1%
3  ping                 7        4.9%

Total: 143 tokens (estimated, offline heuristic).
```

`--markdown` for a report artifact; `--budget N` to fail CI when the surface exceeds
a token budget. Tokenization is an offline heuristic (no model download). See
[docs/phase2-token-report.md](docs/phase2-token-report.md).

### Output trimming (Phase 3)
Stop returning fields the model never reads.

```csharp
[HttpGet("{id}")]
[McpTool]
[McpToolOutput(Fields = new[] { "id", "status" }, MaxLength = 500)]
public OrderDto GetOrder(int id) => ...;
```

The generated tool projects the response to the listed top-level fields, then
truncates to `MaxLength`. Best-effort: malformed JSON passes through untouched.

### Safety hints (Phase 4)
Tool annotations (`ReadOnly`, `Destructive`, `Idempotent`) are derived from the HTTP
verb and emitted onto the tool. Exposing a destructive operation (POST/PUT/PATCH/
DELETE) raises `MCPGEN002` unless you acknowledge it:

```csharp
[HttpDelete("{id}")]
[McpTool(AllowDestructive = true)]
public IActionResult Delete(int id) => ...;
```

## Project layout

```
src/
  McpEndpoints.Abstractions   [McpTool], [McpToolOutput] attributes
  McpEndpoints.Generator      Roslyn incremental source generator
  McpEndpoints                runtime invoker, OutputShaper, AddMcpEndpoints DI
  McpEndpoints.TokenReport    offline token-cost analyzer (library)
  McpEndpoints.TokenReport.Tool   mcp-token-report CLI
samples/SampleApi             working example web app
tests/                        generator, runtime, token-report, integration tests
docs/                         design doc, Phase 1 plan, Phase 2 notes
```

## Design

The full design rationale (why build on top of the official SDK, why build-time, the
token-economy focus) is in
[docs/plans/2026-06-06-dotnet-mcp-tool-generator-design.md](docs/plans/2026-06-06-dotnet-mcp-tool-generator-design.md).

## Known limitations (current)

- Controllers only (minimal-API lambda endpoints not yet supported).
- One body parameter per tool.
- No auth/identity forwarding on the loopback call.
- Explicit tool `Name` is not string-escaped (use identifier-safe names).
- XML summaries require `GenerateDocumentationFile=true` (see above).

## Requirements

- .NET 10 SDK. The generator pins `Microsoft.CodeAnalysis.CSharp` to the Roslyn
  version bundled with the SDK; do not bump it past the SDK's version or the
  generator will stop loading (CS9057).

## License

TBD.
