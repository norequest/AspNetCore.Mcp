using System;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace McpIt.Runtime.Tests;

public class HttpClientMcpEndpointInvokerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        public string? LastBody;
        public HttpStatusCode StatusCode = HttpStatusCode.OK;
        public string ResponseBody = "RESPONSE";
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseBody),
            };
        }
    }

    // Builds an invoker whose IHttpContextAccessor exposes the given incoming request headers,
    // so header-forwarding behavior can be exercised.
    private static HttpClientMcpEndpointInvoker WithIncomingHeaders(
        HttpClient http, McpEndpointsOptions options, params (string Name, string Value)[] headers)
    {
        var ctx = new DefaultHttpContext { Request = { Scheme = "https", Host = new HostString("api.example.com") } };
        foreach (var (name, value) in headers)
            ctx.Request.Headers[name] = value;
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return new HttpClientMcpEndpointInvoker(http, accessor, options);
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

    [Fact]
    public async Task ForwardAuthorization_copies_authorization_header_to_loopback()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var invoker = WithIncomingHeaders(
            http,
            new McpEndpointsOptions { ForwardAuthorization = true },
            ("Authorization", "Basic dXNlcjpwYXNz"));

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.True(handler.Last!.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal("Basic dXNlcjpwYXNz", string.Join("", values!));
    }

    [Fact]
    public async Task Default_options_do_not_forward_authorization_header()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var invoker = WithIncomingHeaders(
            http,
            new McpEndpointsOptions(),
            ("Authorization", "Basic dXNlcjpwYXNz"));

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.False(handler.Last!.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task ForwardedHeaders_allowlist_copies_named_headers_only()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var options = new McpEndpointsOptions();
        options.ForwardedHeaders.Add("X-Api-Key");
        var invoker = WithIncomingHeaders(
            http,
            options,
            ("X-Api-Key", "secret-key"),
            ("X-Not-Allowed", "nope"));

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.True(handler.Last!.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("secret-key", string.Join("", values!));
        Assert.False(handler.Last!.Headers.Contains("X-Not-Allowed"));
    }

    [Fact]
    public async Task ForwardedHeaders_matching_is_case_insensitive()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var options = new McpEndpointsOptions();
        options.ForwardedHeaders.Add("authorization");
        var invoker = WithIncomingHeaders(
            http,
            options,
            ("Authorization", "Bearer abc"));

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.True(handler.Last!.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal("Bearer abc", string.Join("", values!));
    }

    [Fact]
    public async Task ForwardedHeaders_forwards_all_values_of_a_multi_value_header()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var options = new McpEndpointsOptions();
        options.ForwardedHeaders.Add("X-Multi");
        var ctx = new DefaultHttpContext { Request = { Scheme = "https", Host = new HostString("api.example.com") } };
        ctx.Request.Headers["X-Multi"] = new Microsoft.Extensions.Primitives.StringValues(new[] { "a", "b" });
        var invoker = new HttpClientMcpEndpointInvoker(
            http, new HttpContextAccessor { HttpContext = ctx }, options);

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.True(handler.Last!.Headers.TryGetValues("X-Multi", out var values));
        Assert.Equal(new[] { "a", "b" }, values!);
    }

    [Fact]
    public async Task ForwardAuthorization_and_allowlist_authorization_do_not_double_add()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var options = new McpEndpointsOptions { ForwardAuthorization = true };
        options.ForwardedHeaders.Add("authorization"); // overlaps the shortcut, different case
        var invoker = WithIncomingHeaders(http, options, ("Authorization", "Bearer abc"));

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.True(handler.Last!.Headers.TryGetValues("Authorization", out var values));
        Assert.Single(values!);
    }

    [Fact]
    public async Task Forwarding_is_a_noop_when_there_is_no_http_context()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        // Explicit base address (so no context is needed to resolve the URL) + forwarding on,
        // but no HttpContext to copy headers from: must not throw and must not add headers.
        var invoker = new HttpClientMcpEndpointInvoker(
            http, new HttpContextAccessor(),
            new McpEndpointsOptions { BaseAddress = new Uri("http://localhost/"), ForwardAuthorization = true });

        var body = await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.Equal("RESPONSE", body);
        Assert.False(handler.Last!.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task ForwardedHeaders_omits_headers_absent_from_the_incoming_request()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler);
        var options = new McpEndpointsOptions();
        options.ForwardedHeaders.Add("X-Api-Key"); // configured, but the incoming request has none
        var invoker = WithIncomingHeaders(http, options, ("Authorization", "Bearer abc"));

        await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.False(handler.Last!.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task ThrowOnUnsuccessfulResponse_throws_with_status_and_body()
    {
        var handler = new CapturingHandler
        {
            StatusCode = HttpStatusCode.Unauthorized,
            ResponseBody = "no credentials",
        };
        var http = new HttpClient(handler);
        var invoker = new HttpClientMcpEndpointInvoker(
            http, new HttpContextAccessor(),
            new McpEndpointsOptions { BaseAddress = new Uri("http://localhost/"), ThrowOnUnsuccessfulResponse = true });

        var ex = await Assert.ThrowsAsync<McpEndpointInvocationException>(() =>
            invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("no credentials", ex.ResponseBody);
    }

    [Fact]
    public async Task Default_options_return_body_on_non_success_without_throwing()
    {
        var handler = new CapturingHandler
        {
            StatusCode = HttpStatusCode.Unauthorized,
            ResponseBody = "401 body",
        };
        var http = new HttpClient(handler);
        var invoker = WithExplicitBase(http, "http://localhost/");

        var body = await invoker.InvokeAsync("GET", "orders/1", null, null, CancellationToken.None);

        Assert.Equal("401 body", body);
    }
}
