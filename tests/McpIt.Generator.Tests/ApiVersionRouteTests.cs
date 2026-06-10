namespace McpIt.Generator.Tests;

public class ApiVersionRouteTests
{
    // Local stand-ins for the Asp.Versioning attributes; the generator matches them by
    // simple type name, so these exercise the real code path without the package.
    private const string VersioningAttrs = """
        namespace Asp.Versioning
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class ApiVersionAttribute : System.Attribute
            {
                public ApiVersionAttribute(string version) { }
            }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public sealed class MapToApiVersionAttribute : System.Attribute
            {
                public MapToApiVersionAttribute(string version) { }
            }
        }
        """;

    // Attribute stand-ins go AFTER the controller so the controller's using directives
    // still lead the compilation unit (usings must precede any namespace declaration).
    private static GeneratorResult Run(string controller) =>
        GeneratorTestHarness.Run(controller + "\n" + VersioningAttrs);

    [Fact]
    public void Controller_api_version_replaces_route_token()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using Asp.Versioning;
            using McpIt;

            [ApiVersion("1.0")]
            [Route("v{version:apiVersion}/account")]
            public class AccountController : ControllerBase
            {
                /// <summary>Account info.</summary>
                [McpTool][HttpGet("info")]
                public string Info() => "ok";
            }
            """);

        // Major-only segment for a .0 version; the route constraint accepts /v1/ for "1.0".
        Assert.Contains("v1/account/info", result.AllGeneratedSource);
        Assert.DoesNotContain("apiVersion", result.AllGeneratedSource);
    }

    [Fact]
    public void Derived_tool_name_is_suffixed_with_version()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using Asp.Versioning;
            using McpIt;

            [ApiVersion("1.0")]
            [Route("v{version:apiVersion}/account")]
            public class AccountController : ControllerBase
            {
                /// <summary>Account info.</summary>
                [McpTool][HttpGet("info")]
                public string Info() => "ok";
            }
            """);

        Assert.Contains("Name = \"info_v1\"", result.AllGeneratedSource);
    }

    [Fact]
    public void MapToApiVersion_overrides_controller_version()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using Asp.Versioning;
            using McpIt;

            [ApiVersion("1.0")]
            [ApiVersion("2.0")]
            [Route("v{version:apiVersion}/account")]
            public class AccountController : ControllerBase
            {
                /// <summary>v2 info.</summary>
                [McpTool][HttpGet("info")][MapToApiVersion("2.0")]
                public string InfoV2() => "ok";
            }
            """);

        Assert.Contains("v2/account/info", result.AllGeneratedSource);
    }

    [Fact]
    public void Multiple_versions_pick_highest()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using Asp.Versioning;
            using McpIt;

            [ApiVersion("1.0")]
            [ApiVersion("3.0")]
            [ApiVersion("2.0")]
            [Route("v{version:apiVersion}/account")]
            public class AccountController : ControllerBase
            {
                /// <summary>Info.</summary>
                [McpTool][HttpGet("info")]
                public string Info() => "ok";
            }
            """);

        Assert.Contains("v3/account/info", result.AllGeneratedSource);
    }

    [Fact]
    public void Non_zero_minor_version_is_preserved()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using Asp.Versioning;
            using McpIt;

            [ApiVersion("2.1")]
            [Route("v{version:apiVersion}/account")]
            public class AccountController : ControllerBase
            {
                /// <summary>Info.</summary>
                [McpTool][HttpGet("info")]
                public string Info() => "ok";
            }
            """);

        Assert.Contains("v2.1/account/info", result.AllGeneratedSource);
        Assert.Contains("Name = \"info_v2_1\"", result.AllGeneratedSource);
    }

    [Fact]
    public void Same_named_controllers_in_different_namespaces_do_not_collide()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using Asp.Versioning;
            using McpIt;

            namespace Api.V1
            {
                [ApiVersion("1.0")]
                [Route("v{version:apiVersion}/account")]
                public class AccountController : ControllerBase
                {
                    /// <summary>Account info v1.</summary>
                    [McpTool][HttpGet("info")]
                    public string Info() => "ok";
                }
            }

            namespace Api.V2
            {
                [ApiVersion("2.0")]
                [Route("v{version:apiVersion}/account")]
                public class AccountController : ControllerBase
                {
                    /// <summary>Account info v2.</summary>
                    [McpTool][HttpGet("info")]
                    public string Info() => "ok";
                }
            }
            """);

        // Both tools generate, with distinct routes and distinct names; no generator failure.
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        Assert.Contains("v1/account/info", result.AllGeneratedSource);
        Assert.Contains("v2/account/info", result.AllGeneratedSource);
        Assert.Contains("Name = \"info_v1\"", result.AllGeneratedSource);
        Assert.Contains("Name = \"info_v2\"", result.AllGeneratedSource);
    }

    [Fact]
    public void Route_without_version_token_is_unchanged()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using McpIt;

            [Route("account")]
            public class AccountController : ControllerBase
            {
                /// <summary>Info.</summary>
                [McpTool][HttpGet("info")]
                public string Info() => "ok";
            }
            """);

        Assert.Contains("account/info", result.AllGeneratedSource);
        Assert.Contains("Name = \"info\"", result.AllGeneratedSource);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "MCPGEN003");
    }

    [Fact]
    public void Version_token_without_attribute_raises_warning()
    {
        var result = Run("""
            using Microsoft.AspNetCore.Mvc;
            using McpIt;

            [Route("v{version:apiVersion}/account")]
            public class AccountController : ControllerBase
            {
                /// <summary>Info.</summary>
                [McpTool][HttpGet("info")]
                public string Info() => "ok";
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "MCPGEN003");
    }
}
