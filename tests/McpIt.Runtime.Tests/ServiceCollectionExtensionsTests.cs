using System;
using Microsoft.Extensions.DependencyInjection;

namespace McpIt.Runtime.Tests;

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

    [Fact]
    public void AddMcpEndpoints_throws_when_forwarding_authorization_without_base_address()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpEndpoints(o => o.ForwardAuthorization = true));
    }

    [Fact]
    public void AddMcpEndpoints_throws_when_forwarding_headers_without_base_address()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpEndpoints(o => o.ForwardedHeaders.Add("X-Api-Key")));
    }

    [Fact]
    public void AddMcpEndpoints_allows_forwarding_with_explicit_base_address()
    {
        var services = new ServiceCollection();
        services.AddMcpEndpoints(o =>
        {
            o.BaseAddress = new Uri("https://localhost:5001/");
            o.ForwardAuthorization = true;
        });
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMcpEndpointInvoker>());
    }
}
