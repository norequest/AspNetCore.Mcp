# Design: URL-segment API versioning support

Date: 2026-06-11
Status: Implemented

## Problem statement

ASP.NET Core URL-segment API versioning (via `Asp.Versioning` or the legacy
`Microsoft.AspNetCore.Mvc.Versioning` package) places a route constraint token of the
form `{version:apiVersion}` into the route template, typically on a controller-level
`[Route]` attribute:

```csharp
[Route("v{version:apiVersion}/account")]
```

At request time ASP.NET Core resolves this token to the negotiated version and routes
correctly. The McpIt generator, however, copied route templates verbatim into the
generated loopback `HttpClient` call. This meant the loopback path retained the
literal token (`/vv{version:apiVersion}/account/info`) and every call returned HTTP 404.

Reproducing a real consumer's setup (the standard convention of separate per-version
controllers in sibling namespaces, e.g. `Api.V1.AccountController` and
`Api.V2.AccountController`) exposed two further failures on top of the 404:

**(a) Generator crash on duplicate hint names.** `RegisterSourceOutput` calls
`AddSource(hint, ...)` and requires each hint name to be unique within the generator.
When `Api.V1.AccountController.Info` and `Api.V2.AccountController.Info` were both
annotated with `[McpTool]`, the generator derived the same hint name
(`AccountController_Info_Tool.g.cs`) for both and threw, emitting zero tools for
the entire compilation.

**(b) MCP tool name collision.** The two `Info` actions derived the same MCP tool name
(`info`) even though they targeted different API versions, so only one tool would
survive registration regardless of the hint-name crash.

All three failures hit simultaneously in any versioned controller setup using the
standard namespace convention, leaving the consumer with a completely empty tool list
and no actionable error.

## Why a runtime option was rejected

The obvious alternative is a single global version set at runtime
(`AddMcpEndpoints(o => o.ApiVersion = "1")` or similar). This does not work for
the common case where a single application hosts both V1 and V2 controllers
simultaneously. A global value cannot pick the right version per tool: a V2-only
action must get `/v2/`, a V1-only action must get `/v1/`, and a controller advertising
both must pick one per action. The version is already declared in the source via
`[ApiVersion]` and `[MapToApiVersion]` attributes. Reading those attributes at
generation time gives each tool its own correct version with no runtime configuration
and no changes to consumer controllers.

## Design

### Version resolution (ModelBuilder.ResolveApiVersion)

Resolution is attempted in priority order:

1. `[MapToApiVersion]` on the action method.
2. `[ApiVersion]` on the action method.
3. `[ApiVersion]` on the containing controller class.

Within each level, when multiple attributes are present the highest version wins
(numeric comparison on major then minor, string tie-break). The lookup is by
**simple type name** (`attr.AttributeClass?.Name`), not by fully qualified name.
This means both the modern `Asp.Versioning.ApiVersionAttribute` and the legacy
`Microsoft.AspNetCore.Mvc.Versioning.ApiVersionAttribute` are matched without any
package reference in the generator itself.

Three constructor shapes are handled:

| Constructor | Example | Result |
|---|---|---|
| `string` | `[ApiVersion("1.0")]` | `"1.0"` |
| `double` | `[ApiVersion(1.0)]` | `"1.0"` |
| `int, int[, status]` | `[ApiVersion(2, 1)]` | `"2.1"` |

Resolution returns `null` when no version attribute is found on the action or its
controller, which is the correct result for controllers that do not participate in
URL-segment versioning.

Relevant code: `ModelBuilder.ResolveApiVersion`, `HighestVersion`,
`ReadVersionFromAttribute` (`src/McpIt.Generator/ModelBuilder.cs`).

### Token substitution (ModelBuilder.SubstituteApiVersionToken)

Once a version string is resolved it is substituted into the route template before the
route is stored in `EndpointModel` or passed to `ParameterClassifier`. Every stage
downstream is therefore unaware that versioning tokens ever existed.

The substitution regex is:

```
\{[^{}]*:apiVersion[^{}]*\}
```

This matches any `{...}` segment containing `:apiVersion` (case-insensitive) and
replaces the whole token with the formatted version segment. The replacement is a
no-op when the route contains no `apiVersion` token (non-versioned apps incur no cost)
and returns the original route unchanged when the resolved version is `null` (in which
case MCPGEN003 is raised; see Diagnostics below).

Relevant code: `SubstituteApiVersionToken`, `FormatVersionSegment`
(`src/McpIt.Generator/ModelBuilder.cs`).

### Segment formatting

URL-segment versioning conventionally renders `1.0` as `/v1/`, not `/v1.0/`. The
`apiVersion` route constraint accepts `/v1/` for an `[ApiVersion("1.0")]` declaration,
which is the empirical basis for this default.

The formatting rule: when the minor component is zero, only the major is emitted;
when the minor is non-zero, the full `major.minor` is emitted.

| Declared version | Emitted segment |
|---|---|
| `"1.0"` | `v1` |
| `"2.0"` | `v2` |
| `"2.1"` | `v2.1` |
| `"3"` (no dot) | `v3` |

`FormatVersionSegment` implements this rule. It is called both during route substitution
and during tool-name derivation.

### Tool naming

A derived tool name (no explicit `[McpTool(Name=...)]` and no class-level `NamePrefix`)
folds the version in as a suffix:

```
{camelCaseMethodName}_v{formattedVersion}
```

Dots in the version segment are replaced with underscores so the name is a valid
identifier fragment:

| Method | Version | Tool name |
|---|---|---|
| `Info` | `1.0` | `info_v1` |
| `Info` | `2.0` | `info_v2` |
| `Info` | `2.1` | `info_v2_1` |

The suffix is only appended to a fully derived name. An explicit `Name` or a
class-level `NamePrefix` means the author is taking control of naming and
disambiguation themselves; no suffix is added in either of those cases.

Relevant code: `ModelBuilder.Build`, lines that compute `toolName` and apply the
`_v` suffix (`src/McpIt.Generator/ModelBuilder.cs`).

### Generated-file uniqueness (McpToolGenerator)

`spc.AddSource` requires a unique hint name per generator invocation. Previously the
hint was just `{GeneratedClassName}.g.cs`, which collapsed to
`AccountController_Info_Tool.g.cs` for both `Api.V1.AccountController.Info` and
`Api.V2.AccountController.Info`.

The fix qualifies the hint with the containing namespace:

```csharp
var hint = string.IsNullOrEmpty(model.Namespace)
    ? $"{model.GeneratedClassName}.g.cs"
    : $"{model.Namespace}.{model.GeneratedClassName}.g.cs";
```

This produces `Api.V1.AccountController_Info_Tool.g.cs` and
`Api.V2.AccountController_Info_Tool.g.cs`, which are always distinct when the
controllers live in different namespaces (as the standard per-version-controller
convention requires). Global-namespace controllers retain the old shorter hint, so
there is no change for apps that do not use versioned namespaces.

Relevant code: `McpToolGenerator.Initialize`, hint-name block
(`src/McpIt.Generator/McpToolGenerator.cs`).

### Diagnostic MCPGEN003

If a route still contains an `apiVersion` substring after substitution, it means the
tool's action and controller had no version attribute and the token was left in place.
The loopback call will 404 at runtime. The generator reports this as a warning:

```
MCPGEN003  the route for MCP tool '{0}' contains a {version:apiVersion} token but no
           [ApiVersion]/[MapToApiVersion] was found, so the loopback call will 404;
           add a version attribute to the action or controller
```

The check is in `McpToolGenerator.RegisterSourceOutput`, run on the already-substituted
`model.RouteTemplate`. MCPGEN003 is a warning (not an error) so it does not break
builds; it surfaces clearly in IDE and CI output to guide the consumer to the fix.

Relevant code: `Diagnostics.UnresolvedApiVersion` (`src/McpIt.Generator/Diagnostics.cs`),
`McpToolGenerator.Initialize` MCPGEN003 block (`src/McpIt.Generator/McpToolGenerator.cs`).

## Verification

**Unit tests** (`tests/McpIt.Generator.Tests/ApiVersionRouteTests.cs`) cover:

- Controller-level `[ApiVersion]` replaces the route token; major-only segment is used
  for a `.0` version.
- Derived tool name carries the `_v{N}` suffix.
- Method-level `[MapToApiVersion]` overrides the controller-level version.
- Multiple `[ApiVersion]` declarations on a controller: highest wins.
- Non-zero minor version is preserved in both the route and the tool name
  (`v2.1`, `info_v2_1`).
- Two `AccountController` classes in sibling namespaces (`Api.V1`, `Api.V2`) generate
  without error and produce distinct routes (`v1/account/info`, `v2/account/info`) and
  distinct tool names (`info_v1`, `info_v2`). This is the direct regression test for
  the hint-name collision and name-collision bugs.
- A route with no `{version:apiVersion}` token is passed through unchanged and raises
  no MCPGEN003.
- A route with the token but no version attribute raises exactly MCPGEN003.

The generator test harness defines local stand-ins for `Asp.Versioning.ApiVersionAttribute`
and `MapToApiVersionAttribute` (matched by simple name, not fully qualified name), which
is what proves the simple-name matching strategy is sufficient.

**End-to-end verification** was performed against a .NET 10 consumer app reproducing the
per-version-controller setup. After the fix, both `info_v1` and `info_v2` appeared in
the MCP tool list and each loopback call returned HTTP 200 from the correct route
(`/v1/account/info` and `/v2/account/info` respectively). Before the fix, the same setup
produced an empty tool list due to the generator crash.

## Limitations and future work

**Header and query-string versioning.** When the consumer uses header-based
(`api-version: 2.0`) or query-string-based (`?api-version=2.0`) versioning, there is no
URL-segment token to substitute. The loopback route is already a plain path; no
transformation is needed. These strategies are compatible with McpIt as-is.

**Fallback for unversioned actions.** It is possible to imagine an MSBuild property
such as `McpItDefaultApiVersion` that is substituted when no version attribute is found,
saving the consumer from having to annotate every controller. This is not implemented.
Consumers in that situation should either add the missing `[ApiVersion]` attribute or
suppress MCPGEN003 with `#pragma warning disable MCPGEN003` if the route token is
intentionally left unresolved (for example, when the loopback is never exercised in
their usage pattern).
