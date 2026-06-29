using NexusAI.McpGateway.Configuration;
using NexusAI.McpGateway.Services;
using NexusAI.SharedKernel;
using NexusAI.SharedKernel.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddNexusInfrastructure(
    builder.Configuration,
    "NexusAI.McpGateway",
    includeAspNetCoreTelemetry: true,
    includeRedis: true);

builder.Services.Configure<List<McpServerOptions>>(
    builder.Configuration.GetSection("McpServers"));

builder.Services.AddSingleton<IMcpRegistry, McpRegistry>();

builder.Services.AddNexusKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<IMcpRegistry>();
    await registry.RefreshAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
