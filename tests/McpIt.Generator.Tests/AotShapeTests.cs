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
        // Non-vacuousness guard: confirm a real tool was generated before asserting absence.
        Assert.Contains("getThing", result.AllGeneratedSource);
        Assert.DoesNotContain("global::System.Text.Json.JsonSerializer.Serialize", result.AllGeneratedSource);
    }

    [Fact]
    public void Post_body_tool_uses_reflection_json_today()
    {
        var result = GeneratorTestHarness.Run(PostSource);
        Assert.Contains("createThing", result.AllGeneratedSource);
        Assert.Contains("global::System.Text.Json.JsonSerializer.Serialize", result.AllGeneratedSource);
    }
}
