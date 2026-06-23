using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexusAI.Contracts.Messaging;
using RabbitMQ.Client;

namespace NexusAI.SharedKernel.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory = new()
    {
        Uri = new Uri(configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672")
    };

    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public async Task PublishAsync<T>(string queueName, T message, CancellationToken cancellationToken = default)
    {
        await EnsureChannelAsync(cancellationToken);

        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        logger.LogDebug("Published message to queue {Queue}", queueName);
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            return;
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not null)
            {
                return;
            }

            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _sync.Dispose();
    }
}
