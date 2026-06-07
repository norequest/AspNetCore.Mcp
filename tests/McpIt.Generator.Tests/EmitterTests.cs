using Microsoft.CodeAnalysis;

namespace McpIt.Generator.Tests;

public class EmitterTests
{
    private const string Source = """
        using McpIt;
        using Microsoft.AspNetCore.Mvc;
        namespace Demo;
        public record CreateOrderRequest(string Sku, int Qty);
        [Route("orders")]
        public class OrdersController : ControllerBase
        {
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string GetOrder(int id, string? expand) => "ok";

            /// <summary>Creates an order.</summary>
            [HttpPost]
            [McpTool(Name = "createOrder")]
            public string Create([FromBody] CreateOrderRequest request) => "ok";
        }
        """;

    [Fact]
    public void Generated_tool_has_model_params_and_injected_invoker()
    {
        var result = GeneratorTestHarness.Run(Source);
        var src = result.AllGeneratedSource;

        Assert.Contains("global::McpIt.IMcpEndpointInvoker invoker", src);
        Assert.Contains("global::System.Int32 id", src);                 // fully-qualified, not "int"
        Assert.Contains("global::Demo.CreateOrderRequest request", src);
        Assert.Contains("\"GET\"", src);
        Assert.Contains("\"POST\"", src);
        Assert.Contains("orders/", src);
        Assert.Contains("global::System.Threading.Tasks.Task<string>", src);
    }

    [Fact]
    public void Generated_source_compiles_clean()
    {
        var result = GeneratorTestHarness.Run(Source);
        var errors = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0,
            "Generated code did not compile cleanly:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }
}
