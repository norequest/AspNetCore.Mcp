# .NET MCP Tool Generator: Design Document

**Date:** 2026-06-06
**Status:** Design, pre-implementation
**Goal of the project:** Portfolio and credibility piece. A small, technically deep, demoable .NET library that solves a real and recognized pain, and that nobody has built for .NET yet. Revenue is not the objective.

---

## 1. The problem

AI agents call "tools" that an application exposes over the Model Context Protocol (MCP). Before an agent can use any tool, it first downloads the full tool list: every tool's name, description, and input schema. That list is loaded into the model's limited context window before the user asks anything.

A typical company API has hundreds of endpoints. Exposing them all as tools produces a huge, expensive, noisy tool list. A widely cited example: roughly 40 tools consumed about 72% of a model's context window before any real work began. The result is an agent that is slower, more expensive, and less accurate.

Microsoft maintains the official, GA C# SDK (`ModelContextProtocol` and `ModelContextProtocol.AspNetCore`). It already lets an ASP.NET Core app expose an MCP endpoint with attribute-based tool registration, DI, OAuth, a per-request tool-list filter hook, and pagination. **It does the transport. It does nothing about the bloat.** It ships mechanism, not policy.

There is no .NET library that:
1. Generates MCP tools automatically from an existing API, the way FastMCP and Speakeasy do for Python and TypeScript, and
2. Keeps that output lean, visible in token cost, and safe.

That gap is the opportunity.

---

## 2. What we are building

A .NET library that **automatically turns ASP.NET Core API endpoints into MCP tools at build time**, and makes those tools lean and safe.

The original idea (auto-expose endpoints as MCP tools) is the spine. The differentiators (token visibility, output trimming, safety) are what make auto-generation safe to ship in production, instead of being a naive converter that worsens the bloat problem.

### One-line positioning

> Automatically turn your .NET API into MCP tools. Unlike a naive converter, it shows you the token cost, lets you trim it, and keeps dangerous operations opt-in.

### Hard constraints

- **No external AI calls, ever.** Everything is offline and deterministic: a Roslyn source generator plus a locally bundled tokenizer. No network, no API key, no per-build cost, CI-safe.
- **Consequence:** the library never invents tool descriptions. Descriptions come only from what the developer already wrote (XML doc comments, OpenAPI summaries, attributes). This is a feature: the output is predictable, auditable, and free to run.
- **Build strictly on top of the official SDK.** Never re-implement transport, registration, or auth. Plug into the SDK's extension points. If Microsoft ships an overlapping feature, deprecate that slice and keep the rest.

---

## 3. Developer experience

1. The developer already has an API and is already using the official MCP SDK.
2. They add our package and mark the endpoints they want exposed with an attribute, for example `[McpTool]`. Opt-in, never everything-by-default.
3. They build the project. The source generator reads each marked endpoint's route, HTTP verb, parameters, return type, and existing XML doc comments / OpenAPI summaries, and writes real, editable C# tool files into the project.
4. The build also prints the token bill, for example: "these 12 tools cost 4,800 tokens; this one has no description; this one returns a 40KB object."
5. The generated tools plug straight into the official SDK's MCP endpoint.

### Why this is not the naive-converter anti-pattern

- **Opt-in marking:** no automatic 250-tool explosion.
- **Generated code is real and reviewable:** a human sees every tool in a pull request before it ships.
- **Token report:** cost stays visible at all times.
- **Descriptions are reused, never invented:** no AI, no hallucinated affordances.
- **Destructive verbs are gated:** the agent cannot get a `DELETE` tool by accident.

---

## 4. Architecture

A set of NuGet packages layered on top of `ModelContextProtocol.AspNetCore`. The library owns the layer the SDK deliberately leaves empty: opinionated generation, token economy, and safety.

### Package split

- **`X.Generator`** (Phase 1): Roslyn source generator. Reads endpoint metadata and emits editable `[McpServerTool]` partial classes. Includes build diagnostics (for example, missing-description warnings). Zero runtime footprint.
- **`X.Analyzers`** (Phase 2): token-cost reporting as an MSBuild task / `dotnet` tool, plus IDE diagnostics. Offline tokenizer.
- **`X.Core`** (Phase 3): runtime output projection, truncation, and field selection for tool responses, wired through the SDK's tool invocation path. The only runtime component.
- **`X.Safety`** (Phase 4): auto-annotation of `readOnlyHint` / `destructiveHint` and gating of destructive operations.

### Dependency direction

- Everything depends on `ModelContextProtocol` / `ModelContextProtocol.AspNetCore`.
- `X.Core` and `X.Safety` may depend on shared abstractions.
- `X.Generator` and `X.Analyzers` are compile-time only, no runtime dependency.
- No circular coupling.

### Build vs runtime boundary

- Generators and analyzers run at compile time: no startup cost, AOT-clean.
- Only `X.Core` output shaping runs at runtime, as a thin wrapper around tool invocation.

### Tokenization

- Use `Microsoft.ML.Tokenizers` (.NET-native, supports cl100k / o200k families) locally at build time.
- Counts are clearly labeled estimates.
- Pluggable `ITokenizer` so the library is not married to a single model's tokenizer.
- No exact-count mode that calls an external API. Offline only.

### What the model actually pays for

The unit of token analysis is the serialized `tools/list` payload: each tool's `name` + `description` + the JSON Schema of its `inputSchema` (and `outputSchema` if present). The analyzer tokenizes the exact JSON the SDK would send.

---

## 5. Phased delivery

Each phase is independently shippable and demoable. Phase 1 is a complete, postable project on its own. Each later phase is an "I added X" update that keeps the project visible over time, which is ideal for a portfolio.

| Phase | What ships | Why it matters |
|---|---|---|
| **1. The generator** | `[McpTool]` attribute plus build-time codegen into editable tool files, wired to the official SDK, with basic missing-description warnings | The original idea, realized. Novel in .NET on its own. |
| **2. Token bill** | Build-time token-cost report and IDE diagnostics ("here is what your tools cost") | The attention hook. Screenshot for the README. |
| **3. Output trimming** | Declarative "return only these fields / truncate this" on tool responses | The uncontested moat. Biggest runtime token win, untouched by Microsoft or Anthropic. |
| **4. Safety** | Auto `readOnlyHint` / `destructiveHint`, gate destructive verbs | Demonstrates understanding of the real foot-guns. |

---

## 6. Make-or-break design decisions

1. **Build-time source generation, not runtime reflection.** Generated code is editable, reviewable, AOT-clean, has zero startup cost, and structurally forces human curation, which defeats the tool-bloat anti-pattern. Decided.
2. **Opt-in tool generation, never opt-out.** The default when someone adds the library must be "generate nothing until you mark endpoints." If the default were "expose everything," the library would ship the bloat problem as a feature. Decided.
3. **Differentiate on the layer the giants are not touching.** Token visibility and output-side trimming and safety, all offline and provider-agnostic. Cede transport to Microsoft and cede client-side tool-definition discovery to Anthropic's Tool Search. Decided.

---

## 7. Risks and mitigations

- **Platform risk:** Microsoft owns the SDK and the Anthropic spec relationship and can absorb features. *Mitigation:* stay thin on top of the SDK; value is in opinion, curation, and the unowned output-trimming layer, not in API surface that is trivial to copy. As a portfolio piece, platform risk does not threaten the artifact's value.
- **1:1 mapping anti-pattern:** *Mitigation:* opt-in marking, reviewable generated code, destructive gating, visible token cost. Designed out, not warned about.
- **Scope creep / never finishing:** *Mitigation:* four independent phases; ship Phase 1 first and treat each later phase as optional.
- **Dual-contract versioning (tools coupled to API versions):** *Mitigation:* generated tools are plain, editable code under the developer's control; no hidden runtime mapping to drift.

---

## 8. What this project is explicitly NOT

- Not a new MCP server or transport. The official SDK owns that.
- Not a runtime "point at your OpenAPI and auto-expose everything" converter. That is the anti-pattern.
- Not anything that calls an external AI service.
- Not a commercial venture. It is a portfolio and credibility artifact.

---

## 9. Open items for next session

- Final library name.
- Phase 1 implementation plan (the source generator), to be written with the planning workflow before any code.
- Decide the exact endpoint-metadata source for the generator: Roslyn symbol analysis of marked methods, versus reading generated OpenAPI. Leaning toward Roslyn symbol analysis so it works without an OpenAPI document present.
