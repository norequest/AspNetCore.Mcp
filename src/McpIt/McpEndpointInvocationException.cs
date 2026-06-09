using System;

namespace McpIt;

/// <summary>
/// Thrown by the loopback invoker when a generated tool's call into an endpoint returns a non-2xx
/// status and <see cref="McpEndpointsOptions.ThrowOnUnsuccessfulResponse"/> is enabled. Carries the
/// status code and response body so the failure surfaces as a real error to the MCP client instead
/// of an error page returned as a successful result.
/// </summary>
public sealed class McpEndpointInvocationException : Exception
{
    public McpEndpointInvocationException(string httpMethod, string requestUrl, int statusCode, string responseBody)
        // The URL is deliberately kept out of the message: it can contain query-string secrets and the
        // message often lands in logs. It remains available on RequestUrl for callers that want it.
        : base($"Loopback call {httpMethod} returned HTTP {statusCode}.")
    {
        HttpMethod = httpMethod;
        RequestUrl = requestUrl;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>The HTTP method of the failed loopback call.</summary>
    public string HttpMethod { get; }

    /// <summary>
    /// The absolute URL of the failed loopback call. May contain query-string data, so avoid logging
    /// it verbatim if your routes carry secrets in the query string.
    /// </summary>
    public string RequestUrl { get; }

    /// <summary>The non-2xx HTTP status code returned by the endpoint.</summary>
    public int StatusCode { get; }

    /// <summary>The raw response body returned by the endpoint.</summary>
    public string ResponseBody { get; }
}
