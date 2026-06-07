using Microsoft.CodeAnalysis;

namespace AspNetCore.Mcp.Generator.Tests;

public class SafetyHintTests
{
    private static string Wrap(string method) => $$"""
        using AspNetCore.Mcp;
        using Microsoft.AspNetCore.Mvc;
        namespace Demo;
        public record CreateOrderRequest(string Sku, int Qty);
        [Route("orders")]
        public class OrdersController : ControllerBase { {{method}} }
        """;

    [Fact]
    public void Get_tool_emits_readonly_hint_and_does_not_warn_destructive()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);

        Assert.Contains("ReadOnly = true", result.AllGeneratedSource);
        Assert.Contains("Destructive = false", result.AllGeneratedSource);
        Assert.Contains("Idempotent = true", result.AllGeneratedSource);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "MCPGEN002");
    }

    [Fact]
    public void Post_tool_emits_destructive_hint_and_warns()
    {
        var src = Wrap("""
            /// <summary>Creates an order.</summary>
            [HttpPost]
            [McpTool]
            public string Create([FromBody] CreateOrderRequest request) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);

        Assert.Contains("ReadOnly = false", result.AllGeneratedSource);
        Assert.Contains("Destructive = true", result.AllGeneratedSource);
        Assert.Contains(result.Diagnostics, d => d.Id == "MCPGEN002");
    }

    [Fact]
    public void Delete_tool_warns_destructive()
    {
        var src = Wrap("""
            /// <summary>Deletes an order.</summary>
            [HttpDelete("{id}")]
            [McpTool]
            public string Delete(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);

        Assert.Contains("Destructive = true", result.AllGeneratedSource);
        Assert.Contains(result.Diagnostics, d => d.Id == "MCPGEN002");
    }

    [Fact]
    public void Destructive_tool_with_allow_does_not_warn()
    {
        var src = Wrap("""
            /// <summary>Creates an order.</summary>
            [HttpPost]
            [McpTool(AllowDestructive = true)]
            public string Create([FromBody] CreateOrderRequest request) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);

        Assert.Contains("Destructive = true", result.AllGeneratedSource);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "MCPGEN002");
    }

    [Fact]
    public void Safety_hints_compile_clean()
    {
        var src = Wrap("""
            /// <summary>Creates an order.</summary>
            [HttpPost]
            [McpTool(AllowDestructive = true)]
            public string Create([FromBody] CreateOrderRequest request) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0,
            "Generated code did not compile cleanly:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }
}
