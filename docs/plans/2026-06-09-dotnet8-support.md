# .NET 8/9 Multi-Targeting + AOT Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make McpIt build for and run on .NET 8, 9, and 10 (today net10-only), and turn the unenforced "AOT-safe" claim into a verified, gated guarantee.

**Architecture:** The runtime package multi-targets `net8.0;net9.0;net10.0`. The source generator drops its Roslyn reference from 5.0.0 to 4.8.0 so it loads on the .NET 8/9/10 SDK build hosts (at 5.0.0 it silently emits zero tools on the .NET 8 SDK via CS9057). No production source changes are needed (a full audit found zero net9/net10-only language or BCL usage). Three test projects and the sample multi-target so net8 is actually exercised. AOT is verified two ways: `IsAotCompatible=true` on the runtime (per-TFM trim/AOT analyzer gate) and codegen assertions proving the generated read path is reflection-free while pinning the known reflection-based body path.

**Tech Stack:** .NET SDK 8.0.x/9.0.x/10.0.x, Roslyn (`Microsoft.CodeAnalysis.CSharp`) 4.8.0 for the generator, xUnit, `Basic.Reference.Assemblies`, `Microsoft.AspNetCore.Mvc.Testing`, GitHub Actions.

**Key facts the implementer must respect (do not "fix" these):**
- The generator DLL is hand-packed from `src/McpIt.Generator/bin/$(Configuration)/netstandard2.0/McpIt.Generator.dll` (`src/McpIt/McpIt.csproj:27`). The generator stays `netstandard2.0`; only its Roslyn version changes, so this path is unchanged.
- Public API names `AddMcpEndpoints` / `McpEndpointsOptions` stay as-is.
- The CLI (`src/McpIt.TokenReport.Tool`) and `src/McpIt.TokenReport` stay `net10.0` by decision. `tests/McpIt.TokenReport.Tests` therefore also stays `net10.0`.
- Always verify generator behavior with a CLEAN build (delete `bin`/`obj`); incremental builds cache the generator DLL and can report green on stale output.

---

## Prerequisites

- [ ] **Step 0a: Confirm the three SDKs are installed locally**

Run:
```bash
dotnet --list-sdks
```
Expected: lines beginning `8.0.`, `9.0.`, and `10.0.`. If 8.0 or 9.0 is missing, install them (macOS): `brew install --cask dotnet-sdk` for the latest, and download the 8.0 and 9.0 SDKs from https://dotnet.microsoft.com/download/dotnet if absent. The net8/net9 SDKs are required so `dotnet test` can run those TFM legs locally.

- [ ] **Step 0b: Capture the green baseline (net10 only)**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet build McpIt.slnx -c Release && dotnet test McpIt.slnx -c Release --no-build
```
Expected: build succeeds; all tests pass (the baseline is ~75 tests, all green). Record the count. This is the number that must not regress.

---

## Task 1: Lower the generator's Roslyn reference to 4.8.0

This is the make-or-break change: it lets the generator load on the .NET 8 SDK (Roslyn 4.8) as well as 9 and 10. The generator uses `ForAttributeWithMetadataName` (Roslyn 4.3.1+) and older APIs — with ONE exception that must be fixed first: `EquatableArray.cs:14` initializes an `ImmutableArray<T>` with a collection expression (`[..items]`), which Roslyn 4.8.0 cannot lower (CS9210 — `ImmutableArray<T>` collection-expression support needs the `System.Collections.Immutable` that ships with the 4.9.0 package). Downgrading the Roslyn package downgrades `System.Collections.Immutable` with it, so this line must be rewritten to an equivalent that compiles under 4.8.0.

**Files:**
- Modify: `src/McpIt.Generator/Internal/EquatableArray.cs:14`
- Modify: `src/McpIt.Generator/McpIt.Generator.csproj:12-16`

- [ ] **Step 1: Rewrite the ImmutableArray collection expression so it compiles under Roslyn 4.8.0**

In `src/McpIt.Generator/Internal/EquatableArray.cs`, replace line 14:

```csharp
    public EquatableArray(IEnumerable<T> items) => _array = [..items];
```

with:

```csharp
    public EquatableArray(IEnumerable<T> items) => _array = items.ToImmutableArray();
```

(`using System.Collections.Immutable;` and `using System.Linq;` are already present at the top of the file, so `ToImmutableArray()` resolves. This is semantically identical — it builds an `ImmutableArray<T>` from `items` — but uses a factory method that exists in every `System.Collections.Immutable` version rather than the collection-expression lowering that 4.8.0 lacks.)

- [ ] **Step 2: Replace the Roslyn package reference and its comment**

In `src/McpIt.Generator/McpIt.Generator.csproj`, replace lines 12-16:

```xml
  <ItemGroup>
    <!-- Pinned to match the Roslyn version bundled in the .NET 10 SDK (5.0.x).
         A newer version (e.g. 5.3.0) triggers CS9057 and the generator silently
         fails to load in real builds. -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" PrivateAssets="all" />
  </ItemGroup>
```

with:

```xml
  <ItemGroup>
    <!-- Analyzers must target the LOWEST Roslyn host they support, not the highest.
         4.8.0 is the Roslyn bundled in the .NET 8.0.100 SDK; an analyzer built against
         it loads in the .NET 8, 9, AND 10 SDK compilers. A higher version (e.g. 5.0.0,
         the .NET 10 SDK's Roslyn) raises CS9057 on the .NET 8 SDK and the generator
         silently emits zero tools. The generator uses only ForAttributeWithMetadataName
         (4.3.1+) and older APIs, so 4.8.0 loses no functionality. -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
  </ItemGroup>
```

- [ ] **Step 3: Clean-build the generator and confirm it still compiles under Roslyn 4.8.0**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find src/McpIt.Generator -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet build src/McpIt.Generator/McpIt.Generator.csproj -c Release
```
Expected: build succeeds with no errors. In particular NO `CS9210` (collection-expression) error from `EquatableArray.cs` — if it appears, Step 1 was not applied. (Warnings about analyzer-release-tracking should not appear; if any `RS*` analyzer warning surfaces, it is pre-existing and unrelated.)

- [ ] **Step 4: Clean-build the whole solution and confirm tools still generate on the .NET 10 host**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet build McpIt.slnx -c Release && dotnet test McpIt.slnx -c Release --no-build
```
Expected: build succeeds; the same test count as the Step 0b baseline passes. In particular `EndToEndToolInvocationTests.Generator_produced_tool_types_in_the_real_build` passes (proves the 4.8.0 generator still emits tools under the .NET 10 SDK).

- [ ] **Step 5: Commit**

```bash
git add src/McpIt.Generator/Internal/EquatableArray.cs src/McpIt.Generator/McpIt.Generator.csproj
git commit -m "fix(generator): target Roslyn 4.8.0 so the generator loads on the .NET 8/9 SDK"
```

---

## Task 2: Multi-target the runtime package and add the AOT analyzer gate

**Files:**
- Modify: `src/McpIt/McpIt.csproj:3` (TFM) and `:7` (add AOT props)

- [ ] **Step 1: Change the single TFM to three and enable the AOT/trim analyzer gate**

In `src/McpIt/McpIt.csproj`, replace line 3:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

with:

```xml
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Then, immediately before the closing `</PropertyGroup>` (currently line 7), add:

```xml
    <!-- Turn the "AOT-safe" claim into an enforced gate: IsAotCompatible enables the
         trim, AOT, and single-file analyzers (supported net8.0+). The runtime uses only
         the reflection-free JSON DOM/writer, so this is expected to stay clean. Promote
         the relevant analyzer diagnostics to errors so a future reflection regression
         fails the build instead of slipping through as a warning. -->
    <IsAotCompatible>true</IsAotCompatible>
    <WarningsAsErrors>$(WarningsAsErrors);IL2026;IL2046;IL2050;IL2055;IL2057;IL2070;IL2072;IL2075;IL2080;IL2090;IL3050;IL3051;IL3052;IL3053</WarningsAsErrors>
```

- [ ] **Step 2: Clean-build the runtime across all three TFMs and confirm the AOT gate is green**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find src/McpIt -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet build src/McpIt/McpIt.csproj -c Release
```
Expected: build succeeds for `net8.0`, `net9.0`, and `net10.0` with NO `IL2xxx`/`IL3xxx` errors. If an `IL` error appears, the runtime has an AOT-unsafe call that predates this work — stop and report it; do not silence the diagnostic.

- [ ] **Step 3: Commit**

```bash
git add src/McpIt/McpIt.csproj
git commit -m "feat(runtime): multi-target net8.0;net9.0;net10.0 and add IsAotCompatible AOT gate"
```

---

## Task 3: Multi-target the SampleApi and add a body endpoint to exercise the JSON-body path

The SampleApi must build on net8 because the IntegrationTests reflect over its real build to prove generation works. It is currently GET-only, which is exactly why the reflection-based body-serialization path has never been exercised; we add one POST endpoint so it is.

**Files:**
- Modify: `samples/SampleApi/SampleApi.csproj:3` (TFM) and `:16` (Swashbuckle per-TFM)
- Modify: `samples/SampleApi/Controllers/OrdersController.cs` (add a POST action)

- [ ] **Step 1: Multi-target the sample and make Swashbuckle TFM-conditional**

In `samples/SampleApi/SampleApi.csproj`, replace line 3:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

with:

```xml
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Then replace line 16:

```xml
    <PackageReference Include="Swashbuckle.AspNetCore" Version="10.2.1" />
```

with:

```xml
    <!-- Swashbuckle 10.x targets net10; 7.x is the line that supports net8/net9. -->
    <PackageReference Include="Swashbuckle.AspNetCore" Version="10.2.1" Condition="'$(TargetFramework)' == 'net10.0'" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="7.3.2" Condition="'$(TargetFramework)' != 'net10.0'" />
```

- [ ] **Step 2: Add a POST endpoint with a request body to the controller**

In `samples/SampleApi/Controllers/OrdersController.cs`, insert this action immediately after the `GetOrderTracking` action (after line 58, before the closing `}` of the class on line 59). `AllowDestructive = true` is required because POST is a destructive verb (otherwise diagnostic MCPGEN002 fires).

```csharp

    /// <summary>Adds a note to an order and returns the updated order.</summary>
    // A tool WITH a request body. The [FromBody] parameter is serialized by the generated
    // tool via System.Text.Json. POST is a destructive verb, so AllowDestructive=true is
    // required to acknowledge it (otherwise MCPGEN002 fires at build time).
    [HttpPost("{id}/notes")]
    [McpTool(Name = "addOrderNote", AllowDestructive = true)]
    public ActionResult<Order> AddOrderNote(int id, [FromBody] AddNoteRequest request)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        if (order is null)
            return NotFound();
        // The demo "database" is read-only; echo the order back with the note appended to the item.
        return order with { Item = $"{order.Item} (note: {request.Note})" };
    }
```

Then add this record at the end of the file, immediately after the `Order` record (after line 69):

```csharp

// The request body for addOrderNote. Its shape becomes the MCP tool's body input.
public record AddNoteRequest(string Note);
```

- [ ] **Step 3: Clean-build the sample across all three TFMs**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find samples/SampleApi -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet build samples/SampleApi/SampleApi.csproj -c Release
```
Expected: build succeeds for net8.0, net9.0, and net10.0. If `Swashbuckle.AspNetCore 7.3.2` fails to restore for net8/net9, run `dotnet restore samples/SampleApi/SampleApi.csproj` to see the exact compatible version and pin the nearest 7.x that resolves; re-run the build.

- [ ] **Step 4: Commit**

```bash
git add samples/SampleApi/SampleApi.csproj samples/SampleApi/Controllers/OrdersController.cs
git commit -m "feat(sample): multi-target net8/9/10 and add a POST body endpoint (addOrderNote)"
```

---

## Task 4: Codegen assertions for the AOT read/body distinction

These tests pin the AOT story at the source level: the generated read (GET) path must contain no reflection-based JSON, and the generated body (POST) path is documented to use `JsonSerializer.Serialize` (the known, accurately-scoped limitation). They run against the in-memory generator harness, so no native compile is needed.

**Files:**
- Test: `tests/McpIt.Generator.Tests/AotShapeTests.cs` (create)

- [ ] **Step 1: Write the failing tests**

Create `tests/McpIt.Generator.Tests/AotShapeTests.cs`:

```csharp
namespace McpIt.Generator.Tests;

// Pins the AOT story for generated code at the source level:
//  - GET/read tools must be reflection-free (no JsonSerializer.Serialize).
//  - POST/body tools currently use reflection-based JsonSerializer.Serialize. This is the
//    accurately-scoped, known limitation; the test documents it so a future AOT-safe body
//    rewrite is a deliberate, test-visible change rather than a silent one.
public class AotShapeTests
{
    private const string GetSource = """
        using McpIt;
        using Microsoft.AspNetCore.Mvc;
        [ApiController, Route("things")]
        public class ThingsController : ControllerBase
        {
            /// <summary>Gets a thing.</summary>
            [HttpGet("{id}")]
            [McpTool(Name = "getThing")]
            public IActionResult GetThing(int id) => Ok(id);
        }
        """;

    private const string PostSource = """
        using McpIt;
        using Microsoft.AspNetCore.Mvc;
        public record CreateThing(string Name);
        [ApiController, Route("things")]
        public class ThingsController : ControllerBase
        {
            /// <summary>Creates a thing.</summary>
            [HttpPost]
            [McpTool(Name = "createThing", AllowDestructive = true)]
            public IActionResult CreateThing([FromBody] CreateThing body) => Ok();
        }
        """;

    [Fact]
    public void Get_tool_generates_no_reflection_json()
    {
        var result = GeneratorTestHarness.Run(GetSource);
        Assert.DoesNotContain("JsonSerializer.Serialize", result.AllGeneratedSource);
    }

    [Fact]
    public void Post_body_tool_uses_reflection_json_today()
    {
        var result = GeneratorTestHarness.Run(PostSource);
        Assert.Contains("JsonSerializer.Serialize", result.AllGeneratedSource);
    }
}
```

- [ ] **Step 2: Run the tests to verify they pass against the current emitter**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
dotnet test tests/McpIt.Generator.Tests/McpIt.Generator.Tests.csproj -c Release --filter "FullyQualifiedName~AotShapeTests"
```
Expected: both tests PASS (the emitter emits `JsonSerializer.Serialize` only for body params — see `src/McpIt.Generator/Emitter.cs:140-145`). If `Get_tool_generates_no_reflection_json` fails, the read path regressed and must be investigated before proceeding.

- [ ] **Step 3: Commit**

```bash
git add tests/McpIt.Generator.Tests/AotShapeTests.cs
git commit -m "test(generator): pin AOT read/body codegen distinction"
```

---

## Task 5: Multi-target McpIt.Generator.Tests and select reference assemblies per TFM

**Files:**
- Modify: `tests/McpIt.Generator.Tests/McpIt.Generator.Tests.csproj:4,11,13`
- Modify: `tests/McpIt.Generator.Tests/GeneratorTestHarness.cs:38`

- [ ] **Step 1: Multi-target the project, make reference assemblies TFM-conditional, and set the test-harness Roslyn to 4.11.0**

In `tests/McpIt.Generator.Tests/McpIt.Generator.Tests.csproj`, replace line 4:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

with:

```xml
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Then replace line 11:

```xml
    <PackageReference Include="Basic.Reference.Assemblies.Net100" Version="1.8.8" />
```

with:

```xml
    <PackageReference Include="Basic.Reference.Assemblies.Net80" Version="1.8.8" Condition="'$(TargetFramework)' == 'net8.0'" />
    <PackageReference Include="Basic.Reference.Assemblies.Net90" Version="1.8.8" Condition="'$(TargetFramework)' == 'net9.0'" />
    <PackageReference Include="Basic.Reference.Assemblies.Net100" Version="1.8.8" Condition="'$(TargetFramework)' == 'net10.0'" />
```

Then replace line 13:

```xml
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
```

with:

```xml
    <!-- The test harness requires Microsoft.CodeAnalysis.CSharp >= 4.11.0 because
         Basic.Reference.Assemblies 1.8.8 transitively requires
         Microsoft.CodeAnalysis.Common >= 4.11.0; combining 4.8.0 with Common 4.11.0
         throws a TypeLoadException at test runtime. The shipped generator DLL is
         still built against 4.8.0. The authoritative proof that the 4.8.0 generator
         emits correct tools under a real .NET 8 SDK comes from the net8 leg of
         McpIt.IntegrationTests, not from this harness. -->
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" />
```

- [ ] **Step 2: Select the reference-assembly set per TFM in the harness**

In `tests/McpIt.Generator.Tests/GeneratorTestHarness.cs`, replace line 38:

```csharp
        var references = Net100.References.All.Concat(extraRefs).ToArray();
```

with:

```csharp
#if NET8_0
        var frameworkRefs = Net80.References.All;
#elif NET9_0
        var frameworkRefs = Net90.References.All;
#else
        var frameworkRefs = Net100.References.All;
#endif
        var references = frameworkRefs.Concat(extraRefs).ToArray();
```

- [ ] **Step 3: Run the generator tests on all three TFMs**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find tests/McpIt.Generator.Tests -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet test tests/McpIt.Generator.Tests/McpIt.Generator.Tests.csproj -c Release
```
Expected: tests build and pass for net8.0, net9.0, and net10.0 (xUnit runs each TFM leg). The new `AotShapeTests` and all existing generator tests pass on every leg. If a leg fails to resolve `Basic.Reference.Assemblies.Net80/Net90`, run `dotnet restore` to confirm 1.8.8 publishes those package IDs; they do.

- [ ] **Step 4: Commit**

```bash
git add tests/McpIt.Generator.Tests/McpIt.Generator.Tests.csproj tests/McpIt.Generator.Tests/GeneratorTestHarness.cs
git commit -m "test(generator): multi-target net8/9/10 with per-TFM reference assemblies and Roslyn 4.11.0 harness"
```

---

## Task 6: Multi-target McpIt.Runtime.Tests

**Files:**
- Modify: `tests/McpIt.Runtime.Tests/McpIt.Runtime.Tests.csproj:4`

- [ ] **Step 1: Change the TFM to three**

In `tests/McpIt.Runtime.Tests/McpIt.Runtime.Tests.csproj`, replace line 4:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

with:

```xml
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

- [ ] **Step 2: Run the runtime tests on all three TFMs**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find tests/McpIt.Runtime.Tests -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet test tests/McpIt.Runtime.Tests/McpIt.Runtime.Tests.csproj -c Release
```
Expected: tests build and pass for net8.0, net9.0, and net10.0. No package changes are needed (the project has no TFM-specific dependencies; `Microsoft.AspNetCore.App` resolves per-TFM).

- [ ] **Step 3: Commit**

```bash
git add tests/McpIt.Runtime.Tests/McpIt.Runtime.Tests.csproj
git commit -m "test(runtime): multi-target net8.0;net9.0;net10.0"
```

---

## Task 7: Multi-target McpIt.IntegrationTests (the authoritative net8 generation proof)

This is the test that proves the generator actually emits and the tools actually invoke on net8: it reflects over the real SampleApi build and calls a generated tool through an in-memory server.

**Files:**
- Modify: `tests/McpIt.IntegrationTests/McpIt.IntegrationTests.csproj:4,12`

- [ ] **Step 1: Multi-target and make Mvc.Testing TFM-conditional**

In `tests/McpIt.IntegrationTests/McpIt.IntegrationTests.csproj`, replace line 4:

```xml
    <TargetFramework>net10.0</TargetFramework>
```

with:

```xml
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Then replace line 12:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
```

with:

```xml
    <!-- Mvc.Testing is shipped per shared-framework version; match the test's TFM. -->
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" Condition="'$(TargetFramework)' == 'net8.0'" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" Condition="'$(TargetFramework)' == 'net10.0'" />
```

- [ ] **Step 2: Run the integration tests on all three TFMs (clean build is mandatory here)**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find tests/McpIt.IntegrationTests samples/SampleApi -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet test tests/McpIt.IntegrationTests/McpIt.IntegrationTests.csproj -c Release
```
Expected: tests build and pass for net8.0, net9.0, and net10.0. Critically, on the **net8.0** leg `Generator_produced_tool_types_in_the_real_build` and `Invoking_generated_tool_calls_the_real_endpoint` pass — that is the proof that the 4.8.0 generator emits working tools when the SampleApi is compiled on net8. If the net8 leg shows zero tool types, the generator did not load (revisit Task 1); do not work around it in the test.

- [ ] **Step 3: Commit**

```bash
git add tests/McpIt.IntegrationTests/McpIt.IntegrationTests.csproj
git commit -m "test(integration): multi-target net8/9/10 with per-TFM Mvc.Testing (proves net8 generation)"
```

---

## Task 8: Full clean solution build + test across all TFMs

**Files:** none (verification only)

- [ ] **Step 1: Clean everything and run the entire solution**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet build McpIt.slnx -c Release && dotnet test McpIt.slnx -c Release --no-build
```
Expected: build succeeds with no `IL` errors (AOT gate green) and no CS errors. Test count = the Step 0b baseline PLUS the two new `AotShapeTests` PLUS the extra TFM legs of the three multi-targeted test projects (net8 + net9 legs roughly triple those projects' test counts). All green. If any leg fails, fix it in the owning task before continuing.

- [ ] **Step 2: Confirm pack still produces the three packages (no regression to publishing)**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
dotnet pack src/McpIt.Abstractions -c Release -o artifacts-check
dotnet pack src/McpIt -c Release -o artifacts-check
dotnet pack src/McpIt.TokenReport.Tool -c Release -o artifacts-check
ls artifacts-check
```
Expected: `McpIt.Abstractions.*.nupkg`, `McpIt.*.nupkg`, and `McpIt.TokenReport.Tool.*.nupkg` are produced. Then verify the `McpIt` package now carries net8/net9/net10 lib folders:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
unzip -l artifacts-check/McpIt.*.nupkg | grep -E "lib/net(8|9|10)|analyzers/dotnet/cs"
```
Expected: `lib/net8.0/`, `lib/net9.0/`, `lib/net10.0/` McpIt.dll entries AND `analyzers/dotnet/cs/McpIt.Generator.dll`. Clean up: `rm -rf artifacts-check`.

- [ ] **Step 3: Commit (only if any file changed; pack is read-only so usually nothing to commit)**

```bash
git status --short
# If clean, skip. Otherwise:
# git add -A && git commit -m "chore: verification fixups for multi-target build"
```

---

## Task 9: Add the .NET 8 and 9 SDKs to CI

**Files:**
- Modify: `.github/workflows/ci.yml:15-17`

- [ ] **Step 1: Install all three SDKs in the build-test job**

In `.github/workflows/ci.yml`, replace lines 15-17:

```yaml
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore McpIt.slnx
```

with:

```yaml
      - uses: actions/setup-dotnet@v4
        with:
          # All three SDKs so dotnet test exercises the net8/net9/net10 TFM legs and
          # the generator is verified to load under the .NET 8 SDK's Roslyn 4.8 host.
          dotnet-version: |
            8.0.x
            9.0.x
            10.0.x
      - run: dotnet restore McpIt.slnx
```

Leave the `publish` job's `setup-dotnet` (lines ~29-31) at `10.0.x` — packing uses the latest SDK and the multi-target build runs there fine; no change needed.

- [ ] **Step 2: Validate the workflow YAML is well-formed**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('YAML OK')"
```
Expected: `YAML OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: install .NET 8/9/10 SDKs so net8 build+generation is gated on every push"
```

---

## Task 10: Update the README to state proven net8/9 support and scope the AOT claim accurately

Only after Task 8 is green. The AOT claim must be precise: the generated read path is reflection-free; tools with request bodies serialize via reflection-based `System.Text.Json` today.

**Files:**
- Modify: `README.md` (the AOT/feature lines around `:118` and `:162`, plus wherever target frameworks/"Works on .NET 10" is stated)

- [ ] **Step 1: Locate the exact lines to edit**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
grep -niE "net ?10|\.net 10|aot|trim|reflection|framework" README.md
```
Expected: the matching lines, including the two AOT bullets (`AOT-safe, zero reflection` and `AOT-friendly`) and any ".NET 10" requirement statement. Note the actual line numbers from the output for the next step.

- [ ] **Step 2: Edit the target-framework statement to net8/9/10**

Find the line stating the supported framework (e.g. "Works on .NET 10" / "Requires .NET 10") and change it to:

```markdown
Targets .NET 8, 9, and 10. Builds with the .NET 8 SDK and newer (the source generator loads on the .NET 8/9/10 SDK build hosts).
```

- [ ] **Step 3: Make the AOT bullets precise**

Replace the "AOT-safe, zero reflection" bullet (currently README.md:118) with:

```markdown
3. **AOT-friendly, zero runtime reflection for tool discovery.** It is a source generator, so the tool code exists at build time. The runtime and generated read (GET/HEAD) tools use only reflection-free JSON, and `McpIt` is marked `IsAotCompatible` (the trim/AOT analyzers gate it on every build). Note: tools that take a request body currently serialize it with reflection-based `System.Text.Json`, and the MCP SDK's `WithToolsFromAssembly()` registration is reflection-based — use explicit `.WithTools<…>()` registration for a fully AOT-published app.
```

Replace the "AOT-friendly" bullet (currently README.md:162) with:

```markdown
- **AOT-friendly.** Generation happens at compile time. The library is `IsAotCompatible` and the read path is reflection-free; see the note above for the request-body and tool-registration caveats.
```

- [ ] **Step 4: Confirm the README has no stale "net10-only" wording left**

Run:
```bash
cd /Users/tornikematiashvili/Desktop/McpIt
grep -niE "only.*net ?10|requires .net 10|net10 only" README.md || echo "no stale net10-only wording"
```
Expected: `no stale net10-only wording`.

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: state proven .NET 8/9/10 support and scope the AOT claim precisely"
```

---

## Done criteria

- [ ] `dotnet build McpIt.slnx -c Release` is clean from a fresh `bin`/`obj` wipe, with no `IL2xxx`/`IL3xxx` errors.
- [ ] `dotnet test McpIt.slnx -c Release` passes on the net8.0, net9.0, and net10.0 legs of the three multi-targeted test projects.
- [ ] The net8.0 leg of `EndToEndToolInvocationTests` proves the generator emits and invokes tools (≥3 tool types, `getOrder` returns "Ada Lovelace").
- [ ] `AotShapeTests` pins the read-path-clean / body-path-reflection distinction.
- [ ] The packed `McpIt` nupkg contains `lib/net8.0`, `lib/net9.0`, `lib/net10.0`, and `analyzers/dotnet/cs/McpIt.Generator.dll`.
- [ ] CI installs the .NET 8/9/10 SDKs.
- [ ] README states net8/9/10 support and the precise AOT scope.

## Out of scope (do not do here)

- NuGet version bump / republish (separate release decision).
- Making generated request-body serialization AOT-safe (option B; tracked as a follow-up).
- Any non-net8 roadmap feature (minimal-API lambdas, multi-body params, auth forwarding).
