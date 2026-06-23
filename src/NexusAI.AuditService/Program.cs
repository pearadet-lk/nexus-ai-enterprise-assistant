using NexusAI.AuditService;
using NexusAI.SharedKernel;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNexusInfrastructure(
    builder.Configuration,
    "NexusAI.AuditService",
    includeMessaging: true);

builder.Services.AddSingleton<AuditEventProcessor>();
builder.Services.AddHostedService<AuditConsumerWorker>();

var host = builder.Build();
host.Run();
