using AspNetCore.Mcp;

var builder = WebApplication.CreateBuilder(args);

// --- Your normal REST API ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- The MCP server, as an ADDITIONAL endpoint on the same app ---
// Stateless mode keeps testing simple (no Mcp-Session-Id handshake needed).
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

// The generated tools loop back to this app's own endpoints. The base address is
// detected automatically from each incoming MCP request, so no URL is configured here.
builder.Services.AddMcpEndpoints();

var app = builder.Build();

// REST API + Swagger UI
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

// MCP lives at /mcp (NOT the root), so it doesn't collide with the API/Swagger.
app.MapMcp("/mcp");

// Friendly root: send a browser to the Swagger UI.
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

namespace SampleApi
{
    public partial class Program { }
}
