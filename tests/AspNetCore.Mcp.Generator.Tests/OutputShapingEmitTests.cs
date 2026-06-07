using Microsoft.CodeAnalysis;

namespace AspNetCore.Mcp.Generator.Tests;

public class OutputShapingEmitTests
{
    private static string Wrap(string method) => $$"""
        using AspNetCore.Mcp;
        using Microsoft.AspNetCore.Mvc;
        namespace Demo;
        [Route("orders")]
        public class OrdersController : ControllerBase { {{method}} }
        """;

    [Fact]
    public void Tool_with_output_attribute_generates_async_shaper_call()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            [McpToolOutput(Fields = new[]{"id","status"}, MaxLength = 500)]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        var generated = result.AllGeneratedSource;

        Assert.Contains("async global::System.Threading.Tasks.Task<string>", generated);
        Assert.Contains("global::AspNetCore.Mcp.OutputShaper.Shape", generated);
        Assert.Contains("await invoker.InvokeAsync", generated);
        Assert.Contains("500", generated);
        Assert.Contains("\"id\"", generated);
        Assert.Contains("\"status\"", generated);
    }

    [Fact]
    public void Tool_without_output_attribute_stays_non_async()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        var generated = result.AllGeneratedSource;

        Assert.DoesNotContain("async global::System.Threading.Tasks.Task<string>", generated);
        Assert.DoesNotContain("OutputShaper", generated);
        Assert.Contains("return invoker.InvokeAsync", generated);
    }

    [Fact]
    public void Output_shaping_generated_code_compiles_clean()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            [McpToolOutput(Fields = new[]{"id","status"}, MaxLength = 500)]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0,
            "Generated code did not compile cleanly:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void Output_attribute_only_maxlength_generates_async_with_null_fields()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            [McpToolOutput(MaxLength = 100)]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        var generated = result.AllGeneratedSource;

        Assert.Contains("async global::System.Threading.Tasks.Task<string>", generated);
        Assert.Contains("global::AspNetCore.Mcp.OutputShaper.Shape", generated);
        Assert.Contains("100", generated);
    }
}
