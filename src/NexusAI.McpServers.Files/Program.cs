using ModelContextProtocol.Server;
using NexusAI.McpServers.Files.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<DocumentTools>();

builder.Services.AddSingleton<DocumentTools>();

var app = builder.Build();
app.MapMcp();
app.Run();
