using System;
using Microsoft.Extensions.DependencyInjection;

namespace McpIt;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the loopback invoker that generated MCP tools use to call back into this app's
    /// own endpoints. By default the base address is detected automatically from each incoming
    /// MCP request, so no configuration is required.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">
    /// Optional configuration. Set <see cref="McpEndpointsOptions.BaseAddress"/> only to override
    /// auto-detection (e.g. behind a proxy).
    /// </param>
    public static IServiceCollection AddMcpEndpoints(
        this IServiceCollection services,
        Action<McpEndpointsOptions>? configure = null)
    {
        var options = new McpEndpointsOptions();
        configure?.Invoke(options);

        services.AddHttpContextAccessor();
        services.AddSingleton(options);
        services.AddHttpClient<IMcpEndpointInvoker, HttpClientMcpEndpointInvoker>();

        return services;
    }
}
