using NexusAI.NotificationService;
using NexusAI.SharedKernel;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNexusInfrastructure(
    builder.Configuration,
    "NexusAI.NotificationService");

builder.Services.AddHostedService<NotificationConsumerWorker>();

var host = builder.Build();
host.Run();
