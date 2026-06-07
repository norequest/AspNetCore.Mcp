namespace McpIt.Generator.Tests;

public class ParameterMappingTests
{
    private static string Wrap(string method) => $$"""
        using McpIt;
        using Microsoft.AspNetCore.Mvc;
        namespace Demo;
        public record CreateOrderRequest(string Sku, int Qty);
        [Route("orders")]
        public class OrdersController : ControllerBase
        {
            {{method}}
        }
        """;

    [Fact]
    public void Route_param_detected_and_query_param_classified()
    {
        var src = Wrap("""
            /// <summary>x</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string Get(int id, string? q) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("param id -> ParameterSource.Route", result.AllGeneratedSource);
        Assert.Contains("param q -> ParameterSource.Query", result.AllGeneratedSource);
    }

    [Fact]
    public void Complex_type_becomes_body()
    {
        var src = Wrap("""
            /// <summary>x</summary>
            [HttpPost]
            [McpTool]
            public string Create(CreateOrderRequest request) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("param request -> ParameterSource.Body", result.AllGeneratedSource);
    }

    [Fact]
    public void FromBody_primitive_becomes_body()
    {
        var src = Wrap("""
            /// <summary>x</summary>
            [HttpPost]
            [McpTool]
            public string Create([FromBody] string raw) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("param raw -> ParameterSource.Body", result.AllGeneratedSource);
    }
}
