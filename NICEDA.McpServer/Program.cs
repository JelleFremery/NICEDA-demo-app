using NICEDA.McpServer.Tools;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("ApiService");
builder.Services.AddSingleton<CreateFollowUpTaskTool>();
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<CreateFollowUpTaskTool>();

var app = builder.Build();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
