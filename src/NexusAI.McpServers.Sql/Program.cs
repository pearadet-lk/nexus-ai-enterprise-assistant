using ModelContextProtocol.Server;
using NexusAI.McpServers.Sql.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<ShipmentTools>();

builder.Services.AddSingleton<ShipmentTools>();

var app = builder.Build();
app.MapMcp();
app.Run();
