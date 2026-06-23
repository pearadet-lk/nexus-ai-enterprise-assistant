using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NexusAI.AgentService.Services;
using NexusAI.SharedKernel;

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

var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
    ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = builder.Configuration.GetValue("Keycloak:ValidateAudience", false),
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });

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
