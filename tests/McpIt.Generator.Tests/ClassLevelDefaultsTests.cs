namespace McpIt.Generator.Tests;

public class ClassLevelDefaultsTests
{
    [Fact]
    public void Class_level_NamePrefix_is_prepended_to_derived_tool_name()
    {
        var src = """
            using McpIt;
            using Microsoft.AspNetCore.Mvc;
            namespace Demo;
            [Route("orders")]
            [McpTool(NamePrefix = "orders_")]
            public class OrdersController : ControllerBase
            {
                /// <summary>x</summary>
                [HttpGet("{id}")]
                [McpTool]
                public string GetOrder(int id) => "ok";
            }
            """;
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("\"orders_getOrder\"", result.AllGeneratedSource);
    }

    [Fact]
    public void Class_level_NamePrefix_is_not_applied_when_action_sets_explicit_name()
    {
        var src = """
            using McpIt;
            using Microsoft.AspNetCore.Mvc;
            namespace Demo;
            [Route("orders")]
            [McpTool(NamePrefix = "orders_")]
            public class OrdersController : ControllerBase
            {
                /// <summary>x</summary>
                [HttpGet("{id}")]
                [McpTool(Name = "customName")]
                public string GetOrder(int id) => "ok";
            }
            """;
        var result = GeneratorTestHarness.Run(src);
        Assert.Contains("\"customName\"", result.AllGeneratedSource);
        Assert.DoesNotContain("orders_customName", result.AllGeneratedSource);
    }

    [Fact]
    public void Class_level_AllowDestructive_suppresses_MCPGEN002_for_post_action()
    {
        var src = """
            using McpIt;
            using Microsoft.AspNetCore.Mvc;
            namespace Demo;
            [Route("orders")]
            [McpTool(AllowDestructive = true)]
            public class OrdersController : ControllerBase
            {
                /// <summary>Creates an order.</summary>
                [HttpPost]
                [McpTool]
                public string Create() => "ok";
            }
            """;
        var result = GeneratorTestHarness.Run(src);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "MCPGEN002");
    }

    [Fact]
    public void Optin_preserved_action_without_attribute_not_generated_but_annotated_one_is()
    {
        var src = """
            using McpIt;
            using Microsoft.AspNetCore.Mvc;
            namespace Demo;
            [Route("orders")]
            [McpTool(NamePrefix = "x_")]
            public class OrdersController : ControllerBase
            {
                /// <summary>x</summary>
                [HttpGet("{id}")]
                public string NotExposedAction(int id) => "ok";

                /// <summary>y</summary>
                [HttpGet]
                [McpTool]
                public string ExposedAction() => "ok";
            }
            """;
        var result = GeneratorTestHarness.Run(src);
        // Positive control: the annotated action is generated with the prefix.
        Assert.Contains("\"x_exposedAction\"", result.AllGeneratedSource);
        // Opt-in: the un-annotated action produces no generated tool class.
        Assert.DoesNotContain("NotExposedAction", result.AllGeneratedSource);
        Assert.DoesNotContain("notExposedAction", result.AllGeneratedSource);
    }

    [Fact]
    public void Class_level_McpTool_with_no_annotated_actions_produces_no_tools()
    {
        // The most basic Option B invariant: a class-level [McpTool] sets defaults
        // only. With no action carrying its own [McpTool], nothing is exposed.
        var src = """
            using McpIt;
            using Microsoft.AspNetCore.Mvc;
            namespace Demo;
            [Route("orders")]
            [McpTool(NamePrefix = "orders_")]
            public class OrdersController : ControllerBase
            {
                /// <summary>x</summary>
                [HttpGet("{id}")]
                public string GetOrder(int id) => "ok";

                /// <summary>y</summary>
                [HttpPost]
                public string Create() => "ok";
            }
            """;
        var result = GeneratorTestHarness.Run(src);
        Assert.Empty(result.AllGeneratedSource.Trim());
        Assert.DoesNotContain(result.Diagnostics, d => d.Id is "MCPGEN001" or "MCPGEN002");
    }

    [Fact]
    public void Action_level_AllowDestructive_still_works_without_class_attribute()
    {
        var src = """
            using McpIt;
            using Microsoft.AspNetCore.Mvc;
            namespace Demo;
            [Route("orders")]
            public class OrdersController : ControllerBase
            {
                /// <summary>Creates an order.</summary>
                [HttpPost]
                [McpTool(AllowDestructive = true)]
                public string Create() => "ok";
            }
            """;
        var result = GeneratorTestHarness.Run(src);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "MCPGEN002");
    }
}
