using Microsoft.EntityFrameworkCore;
using NexusAI.ContextService.Data;
using NexusAI.SharedKernel;
using NexusAI.SharedKernel.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddNexusInfrastructure(
    builder.Configuration,
    "NexusAI.ContextService",
    includeAspNetCoreTelemetry: true,
    includeRedis: true,
    includeMessaging: true);

var connectionString = builder.Configuration.GetConnectionString("NexusDb")
    ?? throw new InvalidOperationException("Connection string 'NexusDb' is not configured.");

builder.Services.AddDbContext<NexusDbContext>(options =>
    options.UseSqlServer(connectionString));

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
    var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
    await db.Database.MigrateAsync();
    await ShipmentDataSeeder.SeedAsync(db);
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
