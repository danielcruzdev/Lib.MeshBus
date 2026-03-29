using System.Collections.Concurrent;
using System.Text;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Lib.MeshBus.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of IMeshBusSubscriber using RabbitMQ.Client v7.x async API.
/// </summary>
public class RabbitMqSubscriber : IMeshBusSubscriber
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitMqOptions _options;
    private readonly bool _ownsConnection;
    private readonly ConcurrentDictionary<string, string> _consumerTags = new();
    private bool _disposed;
    private bool _exchangeDeclared;

    /// <summary>
    /// Creates a new RabbitMqSubscriber using existing connection and channel.
    /// </summary>
    /// <param name="ownsConnection">When true, the subscriber closes and disposes the connection on dispose.
    /// Set to false when the connection is managed externally (e.g. by the DI container).</param>
    public RabbitMqSubscriber(IConnection connection, IChannel channel, IMessageSerializer serializer, RabbitMqOptions options, bool ownsConnection = false)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsConnection = ownsConnection;
    }

    /// <inheritdoc />
    public async Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_consumerTags.ContainsKey(topic))
        {
            throw new MeshBusException($"Already subscribed to topic '{topic}'.", new InvalidOperationException(), "RabbitMQ");
        }

        try
        {
            await EnsureExchangeDeclaredAsync(cancellationToken);

            // Declare queue for this topic
            var queueName = $"meshbus.{topic}";
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: _options.Durable,
                exclusive: false,
                autoDelete: _options.AutoDelete,
                arguments: null,
                cancellationToken: cancellationToken);

            // Bind queue to exchange with topic as routing key
            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: _options.ExchangeName,
                routingKey: topic,
                arguments: null,
                cancellationToken: cancellationToken);

            // Create async consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var meshMessage = ConvertToMeshBusMessage<T>(ea, topic);
                    await handler(meshMessage);
                }
                catch
                {
                    // Handler errors are silently consumed to keep the consumer alive
                }
            };

            var consumerTag = await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _consumerTags[topic] = consumerTag;
        }
        catch (Exception ex) when (ex is not MeshBusException)
        {
            throw new MeshBusException(
                $"Failed to subscribe to topic '{topic}': {ex.Message}",
                ex,
                "RabbitMQ");
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_consumerTags.TryRemove(topic, out var consumerTag))
        {
            try
            {
                await _channel.BasicCancelAsync(consumerTag, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                throw new MeshBusException(
                    $"Failed to unsubscribe from topic '{topic}': {ex.Message}",
                    ex,
                    "RabbitMQ");
            }
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(BasicDeliverEventArgs ea, string topic)
    {
        var body = _serializer.Deserialize<T>(ea.Body.ToArray());
        var headers = new Dictionary<string, string>();
        string? correlationId = ea.BasicProperties?.CorrelationId;

        if (ea.BasicProperties?.Headers != null)
        {
            foreach (var header in ea.BasicProperties.Headers)
            {
                if (header.Value is byte[] bytes)
                {
                    headers[header.Key] = Encoding.UTF8.GetString(bytes);
                }
                else if (header.Value != null)
                {
                    headers[header.Key] = header.Value.ToString()!;
                }
            }
        }

        var timestamp = ea.BasicProperties?.Timestamp.UnixTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime)
            : DateTimeOffset.UtcNow;

        return new MeshBusMessage<T>
        {
            Id = ea.BasicProperties?.MessageId ?? Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Headers = headers,
            Body = body!,
            CorrelationId = correlationId,
            Topic = topic
        };
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
            _disposed = true;
            foreach (var kvp in _consumerTags)
            {
                try
                {
                    await _channel.BasicCancelAsync(kvp.Value);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
            _consumerTags.Clear();
            await _channel.CloseAsync();
            _channel.Dispose();
            if (_ownsConnection)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
    }
}

