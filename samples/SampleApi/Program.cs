using McpEndpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
// Stateless mode: no Mcp-Session-Id handshake required, so a single POST works and
// plain GETs don't fail. Simplest for a sample/demo. Drop this for session-based servers.
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();
builder.Services.AddMcpEndpoints(o => o.BaseAddress = new Uri("http://localhost"));

var app = builder.Build();
app.MapControllers();
app.MapMcp();
app.Run();

namespace SampleApi
{
    public partial class Program { }
}
