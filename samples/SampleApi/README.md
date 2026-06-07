# SampleApi: McpIt in 30 seconds

A normal ASP.NET Core Web API that becomes an MCP server with zero hand-written MCP code.
Annotate a controller action with `[McpTool]` and the McpIt source generator emits a Model
Context Protocol tool for it at build time. The generated tools loop back into this same
app's own endpoints, so your REST API and your MCP tools can never drift apart.

## Run it

```bash
dotnet run --project samples/SampleApi
```

On startup Kestrel prints the address it is listening on, for example
`Now listening on: http://localhost:5000`. Use that base address below. The sample exposes:

- REST API + Swagger UI at `/swagger` (the root `/` redirects here)
- MCP server at `/mcp`

## What got generated

Three annotated actions in `Controllers/OrdersController.cs` produce three MCP tools. McpIt
derives the safety hints straight from the HTTP verb (all GETs here, so every tool is marked
read-only and idempotent):

| Tool name          | Endpoint                  | Shows off                                              |
| ------------------ | ------------------------- | ----------------------------------------------------- |
| `listOrders`       | `GET /orders`             | Read-only GET, tool name derived from the method name |
| `getOrder`         | `GET /orders/{id}`        | A typed parameter (`id`) and an explicit tool name    |
| `getOrderTracking` | `GET /orders/{id}/tracking` | `[McpToolOutput]` field projection + length cap      |

`getOrderTracking` returns the full order from the REST endpoint, but `[McpToolOutput]` trims
the MCP response down to just `id`, `status`, and `trackingNumber` before the model sees it.

## Inspect the tools

Point any MCP client at `http://localhost:5000/mcp` (adjust the port to match the console).

The server runs in stateless HTTP mode, so you can list the tools with a single curl, no
session handshake required:

```bash
curl -s http://localhost:5000/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

You should see `listOrders`, `getOrder`, and `getOrderTracking` in the response, each with the
description taken from the action's XML `<summary>` and the input schema taken from its
parameters.

Prefer a UI? Run the MCP Inspector and connect it to the same `/mcp` URL using the
"Streamable HTTP" transport:

```bash
npx @modelcontextprotocol/inspector
```

## Measure the token cost

McpIt ships a small offline analyzer that estimates how many tokens your tool list will spend
in a model's context. Run it straight against the live server:

```bash
dotnet run --project src/McpIt.TokenReport.Tool -- http://localhost:5000/mcp
```

It fetches `tools/list` from the running server and prints a per-tool and total token
estimate. Add `--markdown` for a table, or `--budget 500` to make it exit non-zero when the
total exceeds your budget (handy in CI to catch tool descriptions that have grown too large).
