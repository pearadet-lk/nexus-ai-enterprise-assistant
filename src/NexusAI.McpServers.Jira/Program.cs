using ModelContextProtocol.Server;
using NexusAI.McpServers.Jira.Services;
using NexusAI.McpServers.Jira.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<JiraIssueStore>();
builder.Services.AddSingleton<JiraTools>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<JiraTools>();

var app = builder.Build();
app.MapMcp();
app.Run();
