using System.Text;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Lib.MeshBus.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of IMeshBusPublisher using RabbitMQ.Client v7.x async API.
/// </summary>
public class RabbitMqPublisher : IMeshBusPublisher
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitMqOptions _options;
    private bool _disposed;
    private bool _exchangeDeclared;

    /// <summary>
    /// Creates a new RabbitMqPublisher using existing connection and channel.
    /// </summary>
    public RabbitMqPublisher(IConnection connection, IChannel channel, IMessageSerializer serializer, RabbitMqOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await EnsureExchangeDeclaredAsync(cancellationToken);

            var body = _serializer.Serialize(message.Body);
            var properties = new BasicProperties
            {
                MessageId = message.Id,
                Timestamp = new AmqpTimestamp(message.Timestamp.ToUnixTimeSeconds()),
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = message.CorrelationId,
                Headers = new Dictionary<string, object?>()
            };

            foreach (var header in message.Headers)
            {
                properties.Headers[header.Key] = Encoding.UTF8.GetBytes(header.Value);
            }

            await _channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: message.Topic,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not MeshBusException)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Message}",
                ex,
                "RabbitMQ");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var exceptions = new List<Exception>();

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(message, cancellationToken);
            }
            catch (MeshBusException ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new MeshBusException(
                $"Failed to publish {exceptions.Count} message(s) in batch.",
                new AggregateException(exceptions),
                "RabbitMQ");
        }
    }

    private async Task EnsureExchangeDeclaredAsync(CancellationToken cancellationToken)
    {
        if (_exchangeDeclared) return;

        await _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: _options.ExchangeType,
            durable: _options.Durable,
            autoDelete: _options.AutoDelete,
            arguments: null,
            cancellationToken: cancellationToken);

        _exchangeDeclared = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
            await _connection.CloseAsync();
            _connection.Dispose();
            _disposed = true;
        }
    }
}

