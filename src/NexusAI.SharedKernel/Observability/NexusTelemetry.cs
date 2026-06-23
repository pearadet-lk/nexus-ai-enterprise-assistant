using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusAI.SharedKernel.Messaging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NexusAI.SharedKernel.Observability;

public static class NexusTelemetry
{
    public static IServiceCollection AddNexusOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCore = false)
    {
        var endpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                if (includeAspNetCore)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                tracing
                    .AddHttpClientInstrumentation()
                    .AddSource(serviceName)
                    .AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            })
            .WithMetrics(metrics =>
            {
                if (includeAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                metrics
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
            });

        return services;
    }

    public static IServiceCollection AddNexusRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    public static IServiceCollection AddNexusMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        return services;
    }
}
