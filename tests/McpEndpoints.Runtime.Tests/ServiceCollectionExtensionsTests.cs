using System;
using Microsoft.Extensions.DependencyInjection;

namespace McpEndpoints.Runtime.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMcpEndpoints_with_no_configuration_registers_invoker()
    {
        var services = new ServiceCollection();
        services.AddMcpEndpoints(); // auto-detect base address; no config required
        using var provider = services.BuildServiceProvider();
        var invoker = provider.GetService<IMcpEndpointInvoker>();
        Assert.NotNull(invoker);
    }

    [Fact]
    public void AddMcpEndpoints_with_explicit_base_address_registers_invoker()
    {
        var services = new ServiceCollection();
        services.AddMcpEndpoints(o => o.BaseAddress = new Uri("http://localhost"));
        using var provider = services.BuildServiceProvider();
        var invoker = provider.GetService<IMcpEndpointInvoker>();
        Assert.NotNull(invoker);
    }
}
