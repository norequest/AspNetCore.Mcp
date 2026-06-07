namespace AspNetCore.Mcp.Generator.Tests;

public class ModelExtractionTests
{
    private static string Wrap(string method) => $$"""
        using AspNetCore.Mcp;
        using Microsoft.AspNetCore.Mvc;
        using System.ComponentModel;
        namespace Demo;
        [Route("orders")]
        public class OrdersController : ControllerBase
        {
            {{method}}
        }
        """;

    [Fact]
    public void Combines_class_route_and_method_route()
    {
        var src = Wrap("""
            /// <summary>x</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string GetOrder(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("orders/{id}", result.AllGeneratedSource);
        Assert.Contains("\"GET\"", result.AllGeneratedSource);
    }

    [Fact]
    public void Uses_explicit_tool_name_when_provided()
    {
        var src = Wrap("""
            /// <summary>x</summary>
            [HttpPost]
            [McpTool(Name = "createOrder")]
            public string Create() => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("createOrder", result.AllGeneratedSource);
        Assert.Contains("\"POST\"", result.AllGeneratedSource);
    }

    [Fact]
    public void Derives_camelCase_tool_name_when_not_provided()
    {
        var src = Wrap("""
            /// <summary>x</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string GetOrder(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("getOrder", result.AllGeneratedSource);
    }

    [Fact]
    public void Pulls_description_from_xml_summary()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string GetOrder(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("Gets an order.", result.AllGeneratedSource);
    }
}
