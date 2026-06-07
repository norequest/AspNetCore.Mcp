using System;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace AspNetCore.Mcp.Runtime.Tests;

public class HttpClientMcpEndpointInvokerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        public string? LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("RESPONSE"),
            };
        }
    }

    // Invoker with an explicit base address (no HttpContext needed).
    private static HttpClientMcpEndpointInvoker WithExplicitBase(HttpClient http, string baseAddress) =>
        new(http, new HttpContextAccessor(), new McpEndpointsOptions { BaseAddress = new Uri(baseAddress) });

    // Invoker that auto-detects the base address from the supplied HttpContext.
    private static HttpClientMcpEndpointInvoker WithAutoDetect(HttpClient http, string scheme, string host)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { Request = { Scheme = scheme, Host = new HostString(host) } },
        };
        return new HttpClientMcpEndpointInvoker(http, accessor, new McpEndpointsOptions());
    }

    [Fact]
    public async Task Builds_request_with_path_query_verb_and_returns_body()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var invoker = WithExplicitBase(http, "http://localhost/");

        var body = await invoker.InvokeAsync("GET", "orders/42", "expand=items", null, CancellationToken.None);

        Assert.Equal("RESPONSE", body);
        Assert.Equal(HttpMethod.Get, handler.Last!.Method);
        Assert.Equal("http://localhost/orders/42?expand=items", handler.Last!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Sends_json_body_for_post()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var invoker = WithExplicitBase(http, "http://localhost/");

        await invoker.InvokeAsync("POST", "orders", null, "{\"sku\":\"x\"}", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Last!.Method);
        Assert.Equal("{\"sku\":\"x\"}", handler.LastBody);
    }

    [Fact]
    public async Task Auto_detects_base_url_from_http_context()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var invoker = WithAutoDetect(http, "https", "api.example.com");

        await invoker.InvokeAsync("GET", "orders/7", null, null, CancellationToken.None);

        Assert.Equal("https://api.example.com/orders/7", handler.Last!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Explicit_base_address_overrides_auto_detection()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        // Has an HttpContext, but an explicit BaseAddress must win.
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { Request = { Scheme = "https", Host = new HostString("ignored.example.com") } },
        };
        var invoker = new HttpClientMcpEndpointInvoker(
            http, accessor, new McpEndpointsOptions { BaseAddress = new Uri("http://configured:9000/") });

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.Equal("http://configured:9000/orders/1", handler.Last!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Throws_helpful_error_when_no_context_and_no_base_address()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var invoker = new HttpClientMcpEndpointInvoker(http, new HttpContextAccessor(), new McpEndpointsOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None));
    }
}
