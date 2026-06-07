using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AspNetCore.Mcp;

public sealed class HttpClientMcpEndpointInvoker : IMcpEndpointInvoker
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly McpEndpointsOptions _options;

    public HttpClientMcpEndpointInvoker(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        McpEndpointsOptions options)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    public async Task<string> InvokeAsync(
        string httpMethod,
        string relativePath,
        string? queryString,
        string? jsonBody,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = ResolveBaseUrl();
        var path = relativePath.TrimStart('/');
        var url = $"{baseUrl}/{path}";
        if (!string.IsNullOrEmpty(queryString))
            url += "?" + queryString;

        using var request = new HttpRequestMessage(new HttpMethod(httpMethod), url);
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return content;
    }

    /// <summary>
    /// Resolves the absolute base URL for loopback calls: the explicitly configured
    /// <see cref="McpEndpointsOptions.BaseAddress"/> when set, otherwise the scheme + host of the
    /// in-flight MCP request (auto-detection).
    /// </summary>
    private string ResolveBaseUrl()
    {
        if (_options.BaseAddress is not null)
            return _options.BaseAddress.ToString().TrimEnd('/');

        var req = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException(
                "AspNetCore.Mcp could not determine the host automatically because there is no active " +
                "HttpContext (the tool was invoked outside of an HTTP request). Set " +
                "McpEndpointsOptions.BaseAddress explicitly in AddMcpEndpoints(...).");

        return $"{req.Scheme}://{req.Host.Value}{req.PathBase.Value}";
    }
}
