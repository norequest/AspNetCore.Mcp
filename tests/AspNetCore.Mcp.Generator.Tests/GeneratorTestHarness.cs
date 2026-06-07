using System.Collections.Immutable;
using Basic.Reference.Assemblies;
using AspNetCore.Mcp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AspNetCore.Mcp.Generator.Tests;

public sealed record GeneratorResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    string AllGeneratedSource);

public static class GeneratorTestHarness
{
    public static GeneratorResult Run(string source)
    {
        var parseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.Parse);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "Input.cs");

        var extraTypes = new[]
        {
            typeof(McpToolAttribute),
            typeof(ModelContextProtocol.Server.McpServerToolAttribute),
            typeof(Microsoft.AspNetCore.Mvc.ControllerBase),
            typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute),
            typeof(Microsoft.AspNetCore.Mvc.RouteAttribute),
            typeof(Microsoft.AspNetCore.Mvc.ApiControllerAttribute),
            typeof(Microsoft.AspNetCore.Mvc.FromBodyAttribute),
            typeof(AspNetCore.Mcp.IMcpEndpointInvoker),
        };

        var extraRefs = extraTypes
            .Select(t => t.Assembly.Location)
            .Distinct()
            .Select(loc => (MetadataReference)MetadataReference.CreateFromFile(loc));

        var references = Net100.References.All.Concat(extraRefs).ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests.Generated",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new McpToolGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);

        var ranDriver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);

        var generated = string.Join(
            "\n\n",
            outputCompilation.SyntaxTrees
                .Where(t => t.FilePath != "Input.cs")
                .Select(t => t.ToString()));

        var compilationDiagnostics = outputCompilation.GetDiagnostics();

        return new GeneratorResult(diagnostics, compilationDiagnostics, generated);
    }
}
