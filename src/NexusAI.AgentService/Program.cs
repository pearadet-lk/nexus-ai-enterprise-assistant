using NexusAI.AgentService.Services;
using NexusAI.SharedKernel;
using NexusAI.SharedKernel.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddNexusInfrastructure(
    builder.Configuration,
    "NexusAI.AgentService",
    includeAspNetCoreTelemetry: true);

builder.Services.AddHttpClient<IContextApiClient, ContextApiClient>((sp, client) =>
{
    var baseUrl = builder.Configuration["Services:ContextService"]
        ?? "http://localhost:5002";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient<IMcpGatewayClient, McpGatewayClient>((sp, client) =>
{
    var baseUrl = builder.Configuration["Services:McpGateway"]
        ?? "http://localhost:5004";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddScoped<ChatRequestContext>();
builder.Services.AddNexusKernel(builder.Configuration);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
