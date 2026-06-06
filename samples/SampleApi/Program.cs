using McpEndpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();
builder.Services.AddMcpEndpoints(o => o.BaseAddress = new Uri("http://localhost"));

var app = builder.Build();
app.MapControllers();
app.MapMcp();
app.Run();

public partial class Program { }
