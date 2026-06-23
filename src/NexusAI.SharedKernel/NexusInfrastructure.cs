using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusAI.SharedKernel.Messaging;
using NexusAI.SharedKernel.Observability;

namespace NexusAI.SharedKernel;

public static class NexusInfrastructure
{
    public static IServiceCollection AddNexusInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCoreTelemetry = false,
        bool includeRedis = false,
        bool includeMessaging = false)
    {
        services.AddNexusOpenTelemetry(configuration, serviceName, includeAspNetCoreTelemetry);

        if (includeRedis)
        {
            services.AddNexusRedisCache(configuration);
        }

        if (includeMessaging)
        {
            services.AddNexusMessaging();
        }

        return services;
    }
}
