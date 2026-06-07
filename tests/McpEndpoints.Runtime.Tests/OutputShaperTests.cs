namespace McpEndpoints.Runtime.Tests;

public class OutputShaperTests
{
    [Fact]
    public void Projects_object_to_selected_fields()
    {
        var json = """{"id":1,"status":"open","secret":"x","note":"y"}""";
        var shaped = OutputShaper.Shape(json, null, new[] { "id", "status" });

        Assert.Contains("\"id\":1", shaped);
        Assert.Contains("\"status\":\"open\"", shaped);
        Assert.DoesNotContain("secret", shaped);
        Assert.DoesNotContain("note", shaped);
    }

    [Fact]
    public void Projects_array_elements_to_selected_fields()
    {
        var json = """[{"id":1,"status":"open","x":9},{"id":2,"status":"done","x":8}]""";
        var shaped = OutputShaper.Shape(json, null, new[] { "id", "status" });

        Assert.Contains("\"id\":1", shaped);
        Assert.Contains("\"id\":2", shaped);
        Assert.Contains("\"status\":\"done\"", shaped);
        Assert.DoesNotContain("\"x\"", shaped);
    }

    [Fact]
    public void Truncates_to_max_length()
    {
        var json = "abcdefghij";
        var shaped = OutputShaper.Shape(json, 4, null);
        Assert.Equal("abcd", shaped);
    }

    [Fact]
    public void Projects_then_truncates_in_order()
    {
        var json = """{"id":1,"status":"open","secret":"longvalueignored"}""";
        var shaped = OutputShaper.Shape(json, 8, new[] { "id" });
        // projection gives {"id":1} then truncate to 8 chars
        Assert.Equal("{\"id\":1}".Substring(0, 8), shaped);
    }

    [Fact]
    public void Malformed_json_passes_through_unchanged_when_projecting()
    {
        var json = "not json at all";
        var shaped = OutputShaper.Shape(json, null, new[] { "id" });
        Assert.Equal("not json at all", shaped);
    }

    [Fact]
    public void Malformed_json_still_truncates()
    {
        var json = "not json at all";
        var shaped = OutputShaper.Shape(json, 3, new[] { "id" });
        Assert.Equal("not", shaped);
    }

    [Fact]
    public void No_options_returns_original()
    {
        var json = """{"id":1}""";
        var shaped = OutputShaper.Shape(json, null, null);
        Assert.Equal(json, shaped);
    }

    [Fact]
    public void Empty_fields_array_does_not_project()
    {
        var json = """{"id":1,"status":"open"}""";
        var shaped = OutputShaper.Shape(json, null, new string[0]);
        Assert.Equal(json, shaped);
    }
}
