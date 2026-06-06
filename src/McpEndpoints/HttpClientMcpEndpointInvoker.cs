using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace McpEndpoints;

public sealed class HttpClientMcpEndpointInvoker : IMcpEndpointInvoker
{
    private readonly HttpClient _http;

    public HttpClientMcpEndpointInvoker(HttpClient http) => _http = http;

    public async Task<string> InvokeAsync(
        string httpMethod,
        string relativePath,
        string? queryString,
        string? jsonBody,
        CancellationToken cancellationToken = default)
    {
        var path = relativePath.TrimStart('/');
        var uri = string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";

        using var request = new HttpRequestMessage(new HttpMethod(httpMethod), uri);
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return content;
    }
}
