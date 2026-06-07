namespace AspNetCore.Mcp.Generator.Tests;

public class HarnessSanityTests
{
    [Fact]
    public void Empty_source_produces_no_error_diagnostics_and_no_output()
    {
        var result = GeneratorTestHarness.Run("namespace Demo { class C { } }");
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        Assert.Equal(string.Empty, result.AllGeneratedSource);
    }
}
