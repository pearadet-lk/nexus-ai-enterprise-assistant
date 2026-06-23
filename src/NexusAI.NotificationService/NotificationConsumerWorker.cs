using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexusAI.Contracts.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NexusAI.NotificationService;

public sealed class NotificationConsumerWorker(
    IConfiguration configuration,
    ILogger<NotificationConsumerWorker> logger) : BackgroundService
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
                    queue: NexusQueues.Notifications,
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
                        var message = JsonSerializer.Deserialize<NotificationMessage>(json, JsonOptions);
                        if (message is not null)
                        {
                            logger.LogInformation(
                                "Notification [{Channel}] to {UserId}: {Subject} — {Body}",
                                message.Channel,
                                message.UserId,
                                message.Subject,
                                message.Body);
                        }

                        await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process notification message");
                        await channel.BasicNackAsync(eventArgs.DeliveryTag, false, true, stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: NexusQueues.Notifications,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("Notification consumer listening on {Queue}", NexusQueues.Notifications);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification consumer connection failed. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
