using Microsoft.Extensions.DependencyInjection;

namespace McpEndpoints.Runtime.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMcpEndpoints_throws_when_base_address_missing()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddMcpEndpoints(_ => { }));
    }

    [Fact]
    public void AddMcpEndpoints_registers_invoker()
    {
        var services = new ServiceCollection();
        services.AddMcpEndpoints(o => o.BaseAddress = new Uri("http://localhost"));
        using var provider = services.BuildServiceProvider();
        var invoker = provider.GetService<IMcpEndpointInvoker>();
        Assert.NotNull(invoker);
    }
}
