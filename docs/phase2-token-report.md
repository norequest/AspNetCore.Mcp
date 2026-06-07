# Phase 2: MCP token-cost report

`AspNetCore.Mcp.TokenReport` is an offline, deterministic analyzer that tells you what your
MCP tool surface costs in tokens. An MCP server advertises its tools via a `tools/list`
result, and the model pays tokens for every tool's `name`, `description`, and `inputSchema`
(JSON Schema) on each turn that the tool list is in context. This is your "token bill":
it ranks tools worst-first so you can see and trim the bloat.

## What it does

Given a `tools/list` JSON payload (from a file), it:

1. Parses `tools[].name`, `tools[].description` (optional), and re-serializes
   `tools[].inputSchema` to a compact JSON string (missing schema -> `{}`).
2. Counts tokens for each part with a tokenizer.
3. Produces a ranked report (per-tool total + % of total, sorted descending) plus a grand total.
4. Renders it as screenshot-friendly text or as Markdown.
5. Optionally fails a CI budget gate when the total exceeds a threshold.

## Offline heuristic caveat

Token counts come from `HeuristicTokenizer`, an **offline approximation** of GPT-style BPE
token counts. It performs **no network calls and no I/O**, so it is fully deterministic and
CI-safe. It uses a GPT-like pre-tokenizer regex and charges longer alphanumeric runs at
roughly `ceil(length / 4)` tokens.

This is an **estimate** (expect roughly +/-20% versus a real tokenizer such as tiktoken/cl100k).
It is intended for relative comparison and budgeting, not exact billing. The exact constant is
not important; what is guaranteed is determinism and monotonicity (more text never yields fewer
tokens).

A real BPE-based `ITokenizer` (e.g. backed by `Microsoft.ML.Tokenizers`) could be added as a
**future opt-in**, but it is intentionally out of scope here because its tiktoken vocabularies
may require a network download, which would violate the offline rule.

## Example

```bash
mcp-token-report ./tools-list.json
mcp-token-report ./tools-list.json --markdown
mcp-token-report ./tools-list.json --budget 4000   # exit 1 if over budget (CI gate)
```

Sample text output:

```
This MCP server spends 143 tokens listing 3 tools.

#  Tool            Tokens  % of total
-  ------------  --------  ----------
1  searchOrders       103       72.0%
2  getOrder            33       23.1%
3  ping                 7        4.9%

Total: 143 tokens (estimated, offline heuristic).
```

## Library API

Namespace `AspNetCore.Mcp.TokenReport`:

- `ITokenizer` / `HeuristicTokenizer` -- token counting.
- `ToolDescriptor` -- a parsed tool (name, description, compact input schema JSON).
- `ToolTokenCost` / `TokenReport` -- per-tool breakdown and ranked report
  (`TokenReport.PercentOfTotal(tool)` gives each tool's share).
- `TokenReporter.Analyze(tools, tokenizer)` -- builds the report;
  `TokenReporter.ExceedsBudget(report, budget)` -- the CI budget check.
- `ReportRenderer.RenderText` / `RenderMarkdown` -- output rendering.
- `ToolListParser.Parse(json)` -- reads a `tools/list` payload into `ToolDescriptor`s.

## Future integration

Today this tool consumes a `tools/list` JSON file. As a future integration, the
AspNetCore.Mcp generator could emit a tool manifest at build time that feeds this analyzer
automatically, so the token bill (and the CI budget gate) runs as part of every build with
no manual export step.
