using System;

namespace McpIt;

public sealed class McpEndpointsOptions
{
    /// <summary>
    /// Optional explicit base address for the loopback calls the generated tools make
    /// (e.g. <c>https://localhost:5001/</c>).
    /// <para>
    /// When <c>null</c> (the default), the base address is detected automatically from the
    /// incoming MCP request (its scheme + host), so no configuration is needed. Set this only
    /// when auto-detection is not appropriate, for example behind a proxy that rewrites the host
    /// or when tools are invoked outside of an HTTP request.
    /// </para>
    /// </summary>
    public Uri? BaseAddress { get; set; }
}
