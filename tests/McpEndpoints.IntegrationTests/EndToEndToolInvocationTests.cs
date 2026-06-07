using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McpEndpoints.IntegrationTests;

public class EndToEndToolInvocationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public EndToEndToolInvocationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void Generator_produced_a_tool_type_in_the_real_build()
    {
        var toolTypes = typeof(SampleApi.Controllers.OrdersController).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttributes(inherit: false)
                .Any(a => a.GetType().Name == "McpServerToolTypeAttribute"))
            .ToList();

        Assert.NotEmpty(toolTypes); // generator ran as a real analyzer
    }

    [Fact]
    public void Generated_tool_method_injects_invoker_and_exposes_model_param()
    {
        var invokeMethod = FindToolInvokeMethod();
        var paramNames = invokeMethod.GetParameters().Select(p => p.Name).ToArray();

        Assert.Contains(invokeMethod.GetParameters(), p => p.ParameterType == typeof(IMcpEndpointInvoker));
        Assert.Contains("id", paramNames);
    }

    [Fact]
    public async Task Invoking_generated_tool_calls_the_real_endpoint()
    {
        // Wire the invoker to the in-memory test server's client so the generated
        // tool's loopback call actually reaches the SampleApi endpoint.
        var client = _factory.CreateClient();
        var invoker = new HttpClientMcpEndpointInvoker(client);

        var invokeMethod = FindToolInvokeMethod();

        // Build arguments in declaration order: (IMcpEndpointInvoker, int id, CancellationToken=default)
        var args = invokeMethod.GetParameters().Select(p =>
            p.ParameterType == typeof(IMcpEndpointInvoker) ? (object)invoker :
            p.Name == "id" ? (object)42 :
            p.ParameterType == typeof(CancellationToken) ? (object)CancellationToken.None :
            throw new InvalidOperationException($"Unexpected param {p.Name}:{p.ParameterType}"))
            .ToArray();

        var task = (Task<string>)invokeMethod.Invoke(null, args)!;
        var result = await task;

        Assert.Contains("order-42", result);
    }

    private static MethodInfo FindToolInvokeMethod()
    {
        var toolType = typeof(SampleApi.Controllers.OrdersController).Assembly
            .GetTypes()
            .First(t => t.GetCustomAttributes(inherit: false)
                .Any(a => a.GetType().Name == "McpServerToolTypeAttribute"));

        return toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.GetCustomAttributes(inherit: false)
                .Any(a => a.GetType().Name == "McpServerToolAttribute"));
    }
}
