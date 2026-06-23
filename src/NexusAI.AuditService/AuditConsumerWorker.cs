using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusAI.Contracts.Messaging;
using NexusAI.SharedKernel.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NexusAI.AuditService;

public sealed class AuditConsumerWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<AuditConsumerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672")
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: NexusQueues.Audit,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, eventArgs) =>
                {
                    try
                    {
                        var json = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                        var message = JsonSerializer.Deserialize<AuditLoggedMessage>(json, JsonOptions);
                        if (message is not null)
                        {
                            using var scope = serviceProvider.CreateScope();
                            var processor = scope.ServiceProvider.GetRequiredService<AuditEventProcessor>();
                            await processor.ProcessAsync(message, stoppingToken);

                            if (message.Cost >= 0.01m)
                            {
                                var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();
                                await publisher.PublishAsync(
                                    NexusQueues.Notifications,
                                    new NotificationMessage(
                                        "email",
                                        "NexusAI high token cost alert",
                                        $"Audit {message.AuditId} recorded cost ${message.Cost:F4} for user {message.UserId}.",
                                        message.UserId),
                                    stoppingToken);
                            }
                        }

                        await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process audit message");
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, false, true, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: NexusQueues.Audit,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("Audit consumer listening on {Queue}", NexusQueues.Audit);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit consumer connection failed. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
