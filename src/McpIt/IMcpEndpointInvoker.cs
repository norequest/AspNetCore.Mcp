using System.Threading;
using System.Threading.Tasks;

namespace McpIt;

public interface IMcpEndpointInvoker
{
    Task<string> InvokeAsync(
        string httpMethod,
        string relativePath,
        string? queryString,
        string? jsonBody,
        CancellationToken cancellationToken = default);
}
