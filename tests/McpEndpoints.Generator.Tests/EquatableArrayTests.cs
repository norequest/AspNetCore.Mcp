using McpEndpoints.Generator.Internal;

namespace McpEndpoints.Generator.Tests;

public class EquatableArrayTests
{
    [Fact]
    public void Equal_arrays_with_same_contents_are_equal()
    {
        var a = new EquatableArray<string>(new[] { "x", "y" });
        var b = new EquatableArray<string>(new[] { "x", "y" });
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Arrays_with_different_contents_are_not_equal()
    {
        var a = new EquatableArray<string>(new[] { "x" });
        var b = new EquatableArray<string>(new[] { "y" });
        Assert.NotEqual(a, b);
    }
}
