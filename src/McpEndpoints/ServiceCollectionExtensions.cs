using System;
using Microsoft.Extensions.DependencyInjection;

namespace McpEndpoints;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMcpEndpoints(
        this IServiceCollection services,
        Action<McpEndpointsOptions> configure)
    {
        var options = new McpEndpointsOptions();
        configure(options);
        if (options.BaseAddress is null)
            throw new InvalidOperationException(
                "McpEndpointsOptions.BaseAddress must be set to the host app's absolute base URL.");

        services.AddHttpClient<IMcpEndpointInvoker, HttpClientMcpEndpointInvoker>(client =>
        {
            client.BaseAddress = options.BaseAddress;
        });

        return services;
    }
}
