using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusAI.SharedKernel.Messaging;
using OpenTelemetry.Exporter;
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
        var otelSection = configuration.GetSection("OpenTelemetry");
        var enabled = otelSection.GetValue("Enabled", true);
        var endpoint = otelSection["OtlpEndpoint"];
        var exportToJaeger = enabled && !string.IsNullOrWhiteSpace(endpoint);

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
                    .AddSource(serviceName);

                if (exportToJaeger)
                {
                    tracing.AddOtlpExporter(options => ConfigureJaegerOtlp(options, endpoint!));
                }
            })
            .WithMetrics(metrics =>
            {
                if (includeAspNetCore)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                metrics.AddHttpClientInstrumentation();

                if (exportToJaeger)
                {
                    metrics.AddOtlpExporter(options => ConfigureJaegerOtlp(options, endpoint!));
                }
            });

        return services;
    }

    /// <summary>
    /// Jaeger listens for OTLP/gRPC on port 4317. Do not use the deprecated OpenTelemetry.Exporter.Jaeger package.
    /// </summary>
    private static void ConfigureJaegerOtlp(OtlpExporterOptions options, string endpoint)
    {
        options.Endpoint = new Uri(endpoint);
        options.Protocol = OtlpExportProtocol.Grpc;
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
