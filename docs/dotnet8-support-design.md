# Design: .NET 8/9 multi-targeting + AOT verification

Date: 2026-06-09
Status: Approved (brainstorming → implementation)

## Goal

Make McpIt usable by .NET 8 and .NET 9 consumers (today it is net10-only), and
turn the existing "AOT-safe" README claim into an enforced, verified gate rather
than an assertion. Both must be *proven* (built and tested on net8), not merely
claimed — the same lesson as the previously-unverified minimal-API claim.

"Support .NET 8" has two independent meanings; we deliver both:

- **(a) net8.0/net9.0 apps can consume McpIt** — multi-target the runtime package.
- **(b) developers on the .NET 8 SDK build host can use the generator** — without
  this, a net8-SDK build produces *zero tools silently* (CS9057). Costs one line.

## Investigation findings (what is and isn't a blocker)

1. **MCP SDK — not a blocker.** McpIt already references
   `ModelContextProtocol.AspNetCore` **1.4.0**, which ships `net8.0` binaries for
   Core, main, and the AspNetCore HTTP transport (`MapMcp`/`WithHttpTransport`).
   No version bump, no API change.

2. **Language/BCL — not a blocker for the runtime; one generator-source fix.**
   Full audit of src/samples/tests found zero C# 13/14 features and zero
   net9/net10-only BCL APIs; the runtime/tests/sample compile unchanged under C#
   12 + the net8 BCL with no conditional compilation. **One exception, discovered
   during implementation:** `src/McpIt.Generator/Internal/EquatableArray.cs:14`
   initializes an `ImmutableArray<T>` with a collection expression (`[..items]`).
   Lowering the generator's Roslyn package to 4.8.0 (finding #3) also lowers
   `System.Collections.Immutable`, and 4.8.0 cannot lower a collection expression
   into `ImmutableArray<T>` (CS9210). The line is rewritten to the equivalent
   `items.ToImmutableArray()`, which compiles on every version. This is a
   library/compiler-binding constraint of the package downgrade, not a C#
   language-version issue (hence the language-level audit did not flag it).

3. **Source generator Roslyn pin — the one required fix.** The generator pins
   `Microsoft.CodeAnalysis.CSharp` **5.0.0**, which loads only in a Roslyn ≥5.0
   host (= .NET 10 SDK only). On the .NET 8 SDK (Roslyn 4.8) it fails with CS9057
   and emits zero tools. The generator uses only `ForAttributeWithMetadataName`
   (4.3.1+) and older APIs, so dropping the reference to **4.8.0** makes it load
   on the .NET 8/9/10 SDK build hosts with no API loss and no multi-targeting.
   Analyzer rule: target the *lowest* host you support, not the highest.

4. **AOT claim is currently unenforced.** No csproj sets `IsAotCompatible`,
   `EnableTrimAnalyzer`, or `EnableAotAnalyzer`; the README asserts "AOT-safe"
   three times (`README.md:118,162` + tagline) with nothing gating it. The
   runtime's own JSON path *is* genuinely reflection-free
   (`OutputShaper.cs` uses `JsonDocument` + `Utf8JsonWriter`), so the substance
   holds for the library — but it is not verified, and one generated path is not
   clean (see below). This is a pre-existing gap, independent of net8.

5. **Generated POST/PUT/PATCH body serialization is NOT AOT-clean.**
   `Emitter.cs:144` emits `System.Text.Json.JsonSerializer.Serialize(body)` — the
   reflection-based overload (IL2026/IL3050 under trimming/AOT). The SampleApi is
   GET-only, so this path is never exercised today, which is exactly why the AOT
   claim went unchallenged. Must be addressed for the claim to be accurate.

## Scope — changes by project

| Project | Change |
|---|---|
| `src/McpIt` (runtime, published) | `TargetFramework` → `TargetFrameworks` = `net8.0;net9.0;net10.0`. Add `<IsAotCompatible>true</IsAotCompatible>` (turns on trim/AOT/single-file analyzers per-TFM). |
| `src/McpIt.Generator` | `Microsoft.CodeAnalysis.CSharp` **5.0.0 → 4.8.0**; stays `netstandard2.0`. Fix the misleading "pin to match .NET 10" comment to explain the lowest-host rule. |
| `src/McpIt.Abstractions` | None — already `netstandard2.0`; attribute-only, trivially AOT-safe. |
| `src/McpIt.TokenReport` + `src/McpIt.TokenReport.Tool` | None — stay `net10.0` (CLI keeps single TFM per decision). |
| `samples/SampleApi` | Multi-target `net8.0;net9.0;net10.0`. Per-TFM `PackageReference` where versions differ (e.g. `Swashbuckle.AspNetCore`). Add at least one **POST endpoint with a body** so the body-serialization path is actually exercised and verifiable. |
| `tests/McpIt.Generator.Tests`, `tests/McpIt.Runtime.Tests`, `tests/McpIt.IntegrationTests` | Multi-target `net8.0;net9.0;net10.0`. Per-TFM conditional `PackageReference`: `Basic.Reference.Assemblies.Net80/Net90/Net100` (Generator.Tests); `Microsoft.AspNetCore.Mvc.Testing` 8.x/9.x/10.x (IntegrationTests). `McpIt.Generator.Tests` uses `Microsoft.CodeAnalysis.CSharp` **4.11.0** (the floor required by `Basic.Reference.Assemblies` 1.8.8 which transitively requires `Microsoft.CodeAnalysis.Common >= 4.11.0`; combining 4.8.0 with Common 4.11.0 causes a TypeLoadException at test runtime). The shipped generator DLL is still built against 4.8.0. Select reference assemblies per-TFM in `GeneratorTestHarness.cs` via `#if`. |
| `tests/McpIt.TokenReport.Tests` | None — stays `net10.0` (tests the net10-only `McpIt.TokenReport`). |
| `.github/workflows/ci.yml` | `actions/setup-dotnet` installs **8.0.x, 9.0.x, 10.0.x**. Build/test runs once; `dotnet test` exercises all TFM legs. Treat trim/AOT analyzer warnings (IL2xxx/IL3xxx) as errors for `src/McpIt`. Publish job unchanged. |
| `README.md` | Update **only after green**: state net8/9/10 support; make the AOT claim precise (see AOT scope). |

## AOT verification strategy

Three layers, cheapest-first:

1. **Library analyzer gate (primary, enforced).** `IsAotCompatible=true` on
   `src/McpIt` runs the trim/AOT analyzers on the runtime per-TFM at build time.
   CI treats IL2xxx/IL3xxx as errors for this project. `src/McpIt` uses only the
   reflection-free JSON DOM/writer, so this is expected green and stays green.

2. **Generated-code gate.** Add an AOT-analyzer-enabled context that contains
   `[McpTool]` endpoints (read *and* a body endpoint) so the generated tool code
   is checked by the analyzer. This is where the `Emitter.cs:144`
   `JsonSerializer.Serialize` reflection path surfaces. Resolution options for the
   body path (decided during planning):
   - **(A) Make the claim precise (lower effort):** GET/read tools are
     AOT-clean; tools with request bodies use reflection-based JSON. Document and
     emit accurately; don't overclaim.
   - **(B) Make generated body serialization AOT-safe (higher effort):** harder
     because consumer body types are arbitrary and a `JsonSerializerContext`
     can't be trivially generated for them. Likely a follow-up, not this unit.
   Default for this unit: **(A)** — accurate scoping now, (B) tracked separately.

3. **AOT smoke-publish (optional, end-to-end).** A minimal AOT publish
   (`/p:PublishAot=true`) of a tiny read-only consumer proves the read path
   compiles natively. Note: the SampleApi's `WithToolsFromAssembly()` is
   reflection-based assembly scanning (MCP SDK discovery, *not* McpIt code) and is
   inherently not AOT-clean — so a full AOT publish of the sample is not the
   signal; the smoke target is a read-only consumer using explicit registration.

Documented caveat: McpIt's generated tool *code* (read path) is AOT-clean;
full end-to-end AOT of a consumer app also depends on the MCP SDK registration
mechanism the consumer chooses (`WithToolsFromAssembly` reflection vs explicit
`.WithTools<…>()`).

## Verification chain ("prove it")

1. **Generator loads on net8 SDK** — IntegrationTests build the SampleApi on the
   net8 leg and assert the tool types exist + invoke. If the Roslyn pin were
   wrong this drops to zero tools and fails loudly. This is the authoritative
   net8-generation proof.
2. **Generator.Tests** run with Roslyn 4.11.0 (harness floor set by `Basic.Reference.Assemblies` 1.8.8) and Net80 reference assemblies on the net8 leg — proves codegen against a net8 compilation. Note: the harness Roslyn is 4.11.0, not 4.8.0; the authoritative proof that the 4.8.0 generator DLL works on a real .NET 8 SDK is the IntegrationTests net8 leg.
3. **Full suite per-TFM** — all test projects run on net8/9/10.
4. **CI matrix** — all three SDKs installed; analyzer-as-error gates the AOT claim.

## Risks & mitigations

- **Silent zero-tool on net8 (highest)** → gated by IntegrationTests net8 leg.
- **Mvc.Testing / reference-assembly drift across TFMs** → conditional
  `PackageReference` keyed on `$(TargetFramework)`.
- **AOT analyzer surfaces the body-serialization path** → expected; resolved by
  accurate scoping (option A) so the claim is true, not silenced.
- **net9 is EOL-ish (STS ended ~May 2026)** → still installs in CI; just another
  `lib/` folder, no special handling.

## Explicitly out of scope

- NuGet version bump / republish — a separate release decision after this lands
  and is green.
- Option (B): making generated request-body serialization AOT-safe — tracked as a
  follow-up.
- Any non-net8 feature work (minimal-API lambdas, multi-body params, auth
  forwarding remain separate roadmap items).
