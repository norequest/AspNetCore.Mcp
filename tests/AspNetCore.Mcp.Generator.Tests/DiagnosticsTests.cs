namespace AspNetCore.Mcp.Generator.Tests;

public class DiagnosticsTests
{
    private static string Wrap(string method) => $$"""
        using AspNetCore.Mcp;
        using Microsoft.AspNetCore.Mvc;
        namespace Demo;
        [Route("orders")]
        public class OrdersController : ControllerBase { {{method}} }
        """;

    [Fact]
    public void Warns_when_marked_action_has_no_description()
    {
        var src = Wrap("""
            [HttpGet("{id}")]
            [McpTool]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains(result.Diagnostics, d => d.Id == "MCPGEN001");
    }

    [Fact]
    public void No_warning_when_description_present()
    {
        var src = Wrap("""
            /// <summary>Gets an order.</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string Get(int id) => "ok";
            """);
        var result = GeneratorTestHarness.Run(src);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "MCPGEN001");
    }
}
