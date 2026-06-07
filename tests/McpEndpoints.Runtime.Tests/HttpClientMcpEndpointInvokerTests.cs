using System.Net;

namespace McpEndpoints.Runtime.Tests;

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

    [Fact]
    public async Task Builds_request_with_path_query_verb_and_returns_body()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new System.Uri("http://localhost/") };
        var invoker = new HttpClientMcpEndpointInvoker(http);

        var body = await invoker.InvokeAsync("GET", "orders/42", "expand=items", null, CancellationToken.None);

        Assert.Equal("RESPONSE", body);
        Assert.Equal(HttpMethod.Get, handler.Last!.Method);
        Assert.Equal("http://localhost/orders/42?expand=items", handler.Last!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Sends_json_body_for_post()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new System.Uri("http://localhost/") };
        var invoker = new HttpClientMcpEndpointInvoker(http);

        await invoker.InvokeAsync("POST", "orders", null, "{\"sku\":\"x\"}", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Last!.Method);
        Assert.Equal("{\"sku\":\"x\"}", handler.LastBody);
    }
}
