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
    /// Optional configuration. Set <see cref="McpEndpointsOptions.BaseAddress"/> to override
    /// auto-detection (e.g. behind a proxy). Use <see cref="McpEndpointsOptions.ForwardAuthorization"/>
    /// or <see cref="McpEndpointsOptions.ForwardedHeaders"/> to forward credentials to protected
    /// endpoints; either of those requires an explicit <see cref="McpEndpointsOptions.BaseAddress"/>
    /// or this method throws. Set <see cref="McpEndpointsOptions.ThrowOnUnsuccessfulResponse"/> to
    /// surface non-2xx responses as <see cref="McpEndpointInvocationException"/>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Header forwarding is configured without a pinned <see cref="McpEndpointsOptions.BaseAddress"/>.
    /// </exception>
    public static IServiceCollection AddMcpEndpoints(
        this IServiceCollection services,
        Action<McpEndpointsOptions>? configure = null)
    {
        var options = new McpEndpointsOptions();
        configure?.Invoke(options);

        // Security guard: forwarding credentials to an auto-detected host is unsafe, because the host
        // is derived from the incoming request's client-controlled Host header. Require a pinned
        // BaseAddress so forwarded headers only ever reach a host the app explicitly trusts.
        if ((options.ForwardAuthorization || options.ForwardedHeaders.Count > 0) && options.BaseAddress is null)
            throw new InvalidOperationException(
                "McpIt: forwarding request headers (ForwardAuthorization / ForwardedHeaders) requires an " +
                "explicit McpEndpointsOptions.BaseAddress. Host auto-detection reads the client-controlled " +
                "Host header, so a spoofed host could receive the forwarded credentials. Set " +
                "options.BaseAddress to the host you trust (e.g. new Uri(\"https://localhost:5001/\")).");

        services.AddHttpContextAccessor();
        services.AddSingleton(options);
        services.AddHttpClient<IMcpEndpointInvoker, HttpClientMcpEndpointInvoker>();

        return services;
    }
}
