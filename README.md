# McpIt

Turn your ASP.NET Core API into **token-efficient MCP tools** at build time.

McpIt is a .NET source generator and runtime that layers **on top of** the official
[`ModelContextProtocol`](https://www.nuget.org/packages/ModelContextProtocol) SDK. You mark the
controller actions you want an AI agent to use with `[McpTool]`, and at build time the generator
emits editable MCP tool classes that the official SDK serves over an MCP endpoint. When the agent
calls a tool, it loops back to your real endpoint, so your existing routing, model binding,
validation, and business logic run unchanged.

Everything is **offline and deterministic** — a Roslyn generator plus a bundled tokenizer. No
network calls, no AI calls, no per-build cost. CI-safe.

```
Your ASP.NET Core API  ──[McpTool]──▶  build-time generator  ──▶  MCP tools at /mcp
                                                                       │
                                              AI agent calls tool ─────┘
                                                                       │ loopback HTTP
                                              ◀── your real controller ┘
```

> **Status:** Phases 1–4 complete; 69 tests passing on .NET 10. Pre-release — APIs may change and
> it is not yet published to NuGet (consume via project references for now).

---

## Table of contents

- [Why](#why)
- [Requirements](#requirements)
- [Install](#install)
- [Quick start](#quick-start)
- [How it works](#how-it-works)
- [Features](#features)
  - [Expose endpoints as tools](#1-expose-endpoints-as-tools)
  - [Tool descriptions](#2-tool-descriptions)
  - [Output trimming](#3-output-trimming-phase-3)
  - [Safety hints & destructive guard](#4-safety-hints--destructive-guard-phase-4)
  - [Token-cost analyzer](#5-token-cost-analyzer-phase-2)
- [Configuration](#configuration)
- [Testing your MCP server](#testing-your-mcp-server)
- [Diagnostics](#diagnostics)
- [Known limitations](#known-limitations)
- [Project layout](#project-layout)
- [Building & testing the library](#building--testing-the-library)
- [Design](#design)

---

## Why

AI agents load the full tool list (every tool's name, description, and input schema) into the
model's context **before** the user asks anything. Naively exposing hundreds of API endpoints as
tools balloons that list — a commonly cited example burned ~72% of a 200K context window on tool
definitions alone. The result is slower, costlier, less accurate agents.

McpIt keeps tool exposure **opt-in**, the generated tools **editable and reviewable**, the
output **trimmable**, destructive operations **flagged**, and the **token cost visible**. The
official SDK gives you an MCP endpoint; this gives you a *curated, lean* one.

---

## Requirements

- **.NET 10 SDK** (the only TFM currently targeted; runtime/tests are `net10.0`).
- The generator pins `Microsoft.CodeAnalysis.CSharp` to the Roslyn version bundled with the SDK.
  Do **not** bump it past the SDK's Roslyn version or the generator stops loading (`CS9057`).

---

## Install

> Not on NuGet yet. Once published, install will be a single package:
> `dotnet add package McpIt`. For now, consume via project references.

Reference the projects directly from your API project:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/src/McpIt/McpIt.csproj" />
  <ProjectReference Include="path/to/src/McpIt.Abstractions/McpIt.Abstractions.csproj" />
  <ProjectReference Include="path/to/src/McpIt.Generator/McpIt.Generator.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>

<PropertyGroup>
  <!-- Optional but recommended: lets XML <summary> comments become tool descriptions. -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

The official MCP SDK (`ModelContextProtocol.AspNetCore`, which provides `AddMcpServer`/`MapMcp` and
the tool attributes the generated code uses) comes in **transitively** via `McpIt` — you do
not need to add it yourself. To pin a specific version, add it explicitly:

```bash
dotnet add package ModelContextProtocol.AspNetCore   # optional; only to control the version
```

---

## Quick start

**1. Mark the actions you want the agent to use.** Opt-in, one attribute each:

```csharp
using McpIt;
using Microsoft.AspNetCore.Mvc;

namespace MyApi.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    /// <summary>Gets an order by its id.</summary>     // becomes the tool description
    [HttpGet("{id}")]
    [McpTool(Name = "getOrder")]                          // expose as an MCP tool
    public Order GetOrder(int id) => _repo.Find(id);
}
```

**2. Wire it up once in `Program.cs`:**

```csharp
using McpIt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// The official MCP server. Stateless = simplest for testing (no session handshake).
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithToolsFromAssembly();

// The loopback runtime. Base address is auto-detected from each request — no config needed.
builder.Services.AddMcpEndpoints();

var app = builder.Build();

app.MapControllers();
app.MapMcp("/mcp");   // MCP lives at /mcp; your API and Swagger stay where they are

app.Run();
```

**3. Run it.** You now have your normal REST API **and** an MCP server at `/mcp`. Point any MCP
client (Claude Desktop/Code, MCP Inspector) at `http://localhost:<port>/mcp` and the agent can call
`getOrder`.

---

## How it works

- **Build time.** The Roslyn generator finds methods annotated with `[McpTool]`, reads their HTTP
  verb, route, parameters, and description, and emits one `[McpServerToolType]` class per action
  into your assembly. The official SDK discovers these via `WithToolsFromAssembly()`. The generated
  code is ordinary C# — you can inspect it (enable `<EmitCompilerGeneratedFiles>true</...>`).
- **Run time.** When the agent invokes a tool, the generated method calls `IMcpEndpointInvoker`,
  which makes an in-process HTTP request back to your own endpoint (e.g. `GET /orders/42`). Because
  it goes through the real pipeline, your routing, model binding, filters, and validation all apply.

This is why it's a thin layer: it never re-implements transport (the official SDK does that) or your
endpoints (your app does that).

---

## Features

### 1. Expose endpoints as tools

`[McpTool]` on a controller action opts it in. Parameters are mapped automatically:

- **Route** parameter if the name appears in the route template (`{id}`),
- **Body** if it has `[FromBody]` or is a complex type,
- **Query** otherwise.

```csharp
[HttpGet("{id}")]
[McpTool]                       // tool name derived from the method (camelCase): "getOrder"
public Order GetOrder(int id, string? expand) { ... }   // id -> route, expand -> query

[HttpPost]
[McpTool(Name = "createOrder")] // explicit tool name
public Order Create([FromBody] CreateOrderRequest request) { ... }  // request -> JSON body
```

**Class-level defaults.** You can also put `[McpTool]` on the controller class to set defaults for
its actions. It never exposes anything on its own (actions still need their own `[McpTool]`), so the
opt-in rule is preserved:

```csharp
[Route("api/orders")]
[McpTool(NamePrefix = "orders_", AllowDestructive = true)]   // defaults for this controller
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    [McpTool]                       // -> "orders_getOrder"
    public Order GetOrder(int id) { ... }

    [HttpPost]
    [McpTool]                       // -> "orders_create", destructive guard already acknowledged
    public Order Create([FromBody] CreateOrderRequest request) { ... }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id) { ... }   // no [McpTool] -> NOT a tool
}
```

- `NamePrefix` is prepended to each action's **derived** (camelCase) name. An action that sets an
  explicit `Name` is used verbatim, with no prefix.
- `AllowDestructive` on the class acts as a default: an action is acknowledged destructive if either
  the action or the class sets it.

### 2. Tool descriptions

A good description is the single biggest factor in whether an agent calls a tool correctly. The
generator reads them — it never invents them. In priority order:

1. XML `/// <summary>` (requires `<GenerateDocumentationFile>true</GenerateDocumentationFile>`),
2. `[Description("...")]` (works without the doc file).

If neither is present you get a build warning ([MCPGEN001](#diagnostics)).

### 3. Output trimming (Phase 3)

Stop feeding the model fields it never reads. `[McpToolOutput]` projects and/or truncates the tool's
response:

```csharp
[HttpGet("{id}")]
[McpTool]
[McpToolOutput(Fields = new[] { "id", "status" }, MaxLength = 500)]
public Order GetOrder(int id) { ... }
```

- `Fields` — keep only these top-level JSON properties (for an object response, or for each element
  of an array response).
- `MaxLength` — truncate the (possibly projected) response to this many characters.

Shaping is best-effort: malformed JSON is passed through unchanged, never throwing. Projection
happens first, then truncation.

### 4. Safety hints & destructive guard (Phase 4)

MCP tool annotations are derived from the HTTP verb and emitted on each tool:

| Verb | readOnly | destructive | idempotent |
|------|:--------:|:-----------:|:----------:|
| GET / HEAD | ✅ | ❌ | ✅ |
| POST | ❌ | ✅ | ❌ |
| PUT | ❌ | ✅ | ✅ |
| PATCH | ❌ | ✅ | ❌ |
| DELETE | ❌ | ✅ | ✅ |

Exposing a destructive operation raises a build warning ([MCPGEN002](#diagnostics)) until you
acknowledge it:

```csharp
[HttpDelete("{id}")]
[McpTool(AllowDestructive = true)]
public IActionResult Delete(int id) { ... }
```

### 5. Token-cost analyzer (Phase 2)

See exactly what your tool surface costs the model, and gate it in CI.

```bash
# point it at a running MCP server (it queries tools/list itself)
dotnet run --project src/McpIt.TokenReport.Tool -- http://localhost:5199/mcp

# or at a saved tools/list JSON file
dotnet run --project src/McpIt.TokenReport.Tool -- tools-list.json

# options
  --markdown       # render a Markdown table (good for CI artifacts / PRs)
  --budget N       # exit 1 when total tokens > N (CI budget gate)
```

Example:

```
This MCP server spends 377 tokens listing 3 tools.

#  Tool            Tokens  % of total
-  ------------  --------  ----------
1  searchOrders       327       86.7%
2  getOrder            33        8.8%
3  ping                17        4.5%

Total: 377 tokens (estimated, offline heuristic).
```

> Token counts use an **offline heuristic** tokenizer (no model download) and are estimates
> (~±20%) — ideal for comparing tools and catching bloat, not for exact billing. The tokenizer is
> pluggable (`ITokenizer`) if you later want an exact one.

---

## Configuration

`AddMcpEndpoints()` needs **no configuration** by default: the loopback base address is detected
automatically from each incoming MCP request (its scheme + host). Override it only when
auto-detection is wrong for your environment:

```csharp
// e.g. behind a reverse proxy that rewrites the host, or for tools invoked outside an HTTP request
builder.Services.AddMcpEndpoints(o => o.BaseAddress = new Uri("https://internal-host:5001/"));
```

Stateless vs. session-based MCP transport is configured on the official SDK, not here:

```csharp
builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true);  // simplest
```

---

## Testing your MCP server

An MCP server speaks JSON-RPC over POST (streamable HTTP) — **you can't test it from a browser**.

**MCP Inspector** (visual, recommended):

```bash
npx @modelcontextprotocol/inspector
# Transport: Streamable HTTP, URL: http://localhost:<port>/mcp, then List Tools / call a tool
```

**curl:**

```bash
# list tools
curl -X POST http://localhost:5199/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'

# call a tool (loops back to your real endpoint)
curl -X POST http://localhost:5199/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"getOrder","arguments":{"id":42}}}'
```

**Claude Desktop / Claude Code:** add the server URL (`http://localhost:<port>/mcp`, streamable
HTTP) to your MCP client config and the tools become available to the model.

A runnable example lives in [`samples/SampleApi`](samples/SampleApi): REST API + Swagger UI at the
root, MCP at `/mcp`.

```bash
dotnet run --project samples/SampleApi --urls http://localhost:5199
# browse http://localhost:5199/  -> Swagger UI
```

---

## Diagnostics

| ID | Severity | Meaning | Fix |
|----|----------|---------|-----|
| `MCPGEN001` | Warning | A `[McpTool]` action has no description. | Add a `/// <summary>` (with `GenerateDocumentationFile`) or `[Description]`. |
| `MCPGEN002` | Warning | A destructive operation (POST/PUT/PATCH/DELETE) is exposed as a tool. | Add `[McpTool(AllowDestructive = true)]` to acknowledge. |

---

## Known limitations

- **Controllers only.** Minimal-API lambda endpoints are not yet supported (their routes/handlers
  are runtime expressions).
- **One body parameter per tool.**
- **No auth/identity forwarding.** The loopback call is a fresh HTTP request and does **not** carry
  the original caller's credentials. Securing tool-exposed endpoints / forwarding identity is future
  work — for now, only expose endpoints that are safe to call without the caller's auth context, or
  put authorization in front of the `/mcp` endpoint.
- **Explicit tool `Name` is not string-escaped** — use identifier-safe names.
- **XML summaries require `GenerateDocumentationFile=true`** in the consuming project; `[Description]`
  works without it.

---

## Project layout

```
src/
  McpIt.Abstractions        [McpTool], [McpToolOutput] attributes  (namespace McpIt)
  McpIt.Generator           Roslyn incremental source generator
  McpIt                     runtime: IMcpEndpointInvoker, OutputShaper, AddMcpEndpoints
  McpIt.TokenReport         offline token-cost analyzer (library)
  McpIt.TokenReport.Tool    mcp-token-report CLI
samples/
  SampleApi                        runnable REST API + Swagger + MCP
tests/
  McpIt.Generator.Tests     generator unit / snapshot tests
  McpIt.Runtime.Tests       invoker, OutputShaper, DI
  McpIt.IntegrationTests    end-to-end: generated tool -> real endpoint
  McpIt.TokenReport.Tests   analyzer tests
docs/                              design doc, Phase 1 plan, Phase 2 notes
```

---

## Building & testing the library

```bash
dotnet build McpIt.slnx
dotnet test  McpIt.slnx      # 69 tests
```

> **Note:** This repo uses a source generator. Incremental builds can occasionally reuse a cached
> generator assembly and report green against stale source. When in doubt, build from clean:
>
> ```bash
> find . -type d \( -name bin -o -name obj \) -not -path '*/.claude/*' -prune -exec rm -rf {} +
> dotnet build McpIt.slnx
> dotnet test  McpIt.slnx
> ```
>
> If your IDE (Rider/ReSharper, VS) offers to "adjust namespaces to match folder," **decline it** for
> `src/McpIt.Abstractions` (attributes intentionally live in namespace `McpIt`) and for
> `IsExternalInit.cs` (must stay in `System.Runtime.CompilerServices`). `RootNamespace` and a
> ReSharper guard are configured to prevent this, but a forced "Code Cleanup" can still override them.

---

## Design

The rationale — why build on top of the official SDK, why a build-time generator, and why the focus
is token economy — is in
[`docs/plans/2026-06-06-dotnet-mcp-tool-generator-design.md`](docs/plans/2026-06-06-dotnet-mcp-tool-generator-design.md).
Phase notes: [`docs/phase2-token-report.md`](docs/phase2-token-report.md).

---

## License

[MIT](LICENSE) — free for any use (personal or commercial), no warranty. Just keep the copyright
and license notice.
