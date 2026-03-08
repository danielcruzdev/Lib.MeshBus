using System.Collections.Concurrent;
using System.Text;
using Confluent.Kafka;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Kafka;

/// <summary>
/// Apache Kafka implementation of IMeshBusPublisher.
/// </summary>
public class KafkaPublisher : IMeshBusPublisher
{
    private readonly IProducer<string, byte[]> _producer;
    private readonly IMessageSerializer _serializer;
    private bool _disposed;

    /// <summary>
    /// Creates a new KafkaPublisher using the provided producer and serializer.
    /// </summary>
    public KafkaPublisher(IProducer<string, byte[]> producer, IMessageSerializer serializer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary>
    /// Creates a new KafkaPublisher using KafkaOptions.
    /// </summary>
    public KafkaPublisher(IOptions<KafkaOptions> options, IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = ParseAcks(options.Value.Acks),
            AllowAutoCreateTopics = options.Value.AllowAutoCreateTopics
        };

        _producer = new ProducerBuilder<string, byte[]>(config).Build();
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var kafkaMessage = CreateKafkaMessage(message);
            await _producer.ProduceAsync(message.Topic, kafkaMessage, cancellationToken);
        }
        catch (ProduceException<string, byte[]> ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Error.Reason}",
                ex,
                "Kafka");
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
                "Kafka");
        }
    }

    private Message<string, byte[]> CreateKafkaMessage<T>(MeshBusMessage<T> message)
    {
        var body = _serializer.Serialize(message.Body);
        var kafkaMessage = new Message<string, byte[]>
        {
            Key = message.Id,
            Value = body,
            Headers = new Headers()
        };

        // Map MeshBus headers to Kafka headers
        foreach (var header in message.Headers)
        {
            kafkaMessage.Headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
        }

        // Add correlation ID as a header if present
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            kafkaMessage.Headers.Add("meshbus-correlation-id", Encoding.UTF8.GetBytes(message.CorrelationId));
        }

        // Add timestamp as a header
        kafkaMessage.Headers.Add("meshbus-timestamp", Encoding.UTF8.GetBytes(message.Timestamp.ToString("O")));

        return kafkaMessage;
    }

    private static Acks ParseAcks(string acks) => acks.ToLowerInvariant() switch
    {
        "all" or "-1" => Confluent.Kafka.Acks.All,
        "leader" or "1" => Confluent.Kafka.Acks.Leader,
        "none" or "0" => Confluent.Kafka.Acks.None,
        _ => Confluent.Kafka.Acks.All
    };

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _producer.Dispose();
            _disposed = true;
        }
        return ValueTask.CompletedTask;
    }
}

