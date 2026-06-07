namespace McpIt.Generator.Tests;

public class GeneratorBasicTests
{
    private const string Source = """
        using McpIt;
        using Microsoft.AspNetCore.Mvc;

        namespace Demo;

        [Route("orders")]
        public class OrdersController : ControllerBase
        {
            /// <summary>Gets an order by id.</summary>
            [HttpGet("{id}")]
            [McpTool]
            public string GetOrder(int id) => "ok";
        }
        """;

    [Fact]
    public void Generates_a_tool_type_for_a_marked_action()
    {
        var result = GeneratorTestHarness.Run(Source);
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Contains("McpServerToolType", result.AllGeneratedSource);
        Assert.Contains("GetOrder", result.AllGeneratedSource);
    }
}
