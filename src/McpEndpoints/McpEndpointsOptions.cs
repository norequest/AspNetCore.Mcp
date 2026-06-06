using System;

namespace McpEndpoints;

public sealed class McpEndpointsOptions
{
    /// <summary>Absolute base address of the host app for loopback calls, e.g. https://localhost:5001/.</summary>
    public Uri? BaseAddress { get; set; }
}
