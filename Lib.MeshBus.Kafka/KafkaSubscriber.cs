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
/// Apache Kafka implementation of IMeshBusSubscriber.
/// </summary>
public class KafkaSubscriber : IMeshBusSubscriber
{
    private readonly IConsumer<string, byte[]> _consumer;
    private readonly IMessageSerializer _serializer;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new KafkaSubscriber using the provided consumer and serializer.
    /// </summary>
    public KafkaSubscriber(IConsumer<string, byte[]> consumer, IMessageSerializer serializer)
    {
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary>
    /// Creates a new KafkaSubscriber using KafkaOptions.
    /// </summary>
    public KafkaSubscriber(IOptions<KafkaOptions> options, IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = options.Value.GroupId ?? $"meshbus-{Guid.NewGuid():N}",
            AutoOffsetReset = ParseAutoOffsetReset(options.Value.AutoOffsetReset),
            EnableAutoCommit = options.Value.EnableAutoCommit,
            AllowAutoCreateTopics = options.Value.AllowAutoCreateTopics
        };

        _consumer = new ConsumerBuilder<string, byte[]>(config).Build();
    }

    /// <inheritdoc />
    public Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_activeSubscriptions.ContainsKey(topic))
        {
            throw new MeshBusException($"Already subscribed to topic '{topic}'.", new InvalidOperationException(), "Kafka");
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSubscriptions[topic] = cts;

        _consumer.Subscribe(topic);

        // Start background consume loop
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cts.Token);
                        if (consumeResult?.Message == null) continue;

                        var meshBusMessage = ConvertToMeshBusMessage<T>(consumeResult);
                        await handler(meshBusMessage);
                    }
                    catch (ConsumeException)
                    {
                        // Log and continue — don't break the consume loop for transient errors
                        if (cts.Token.IsCancellationRequested) break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when unsubscribing
            }
        }, cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_activeSubscriptions.TryRemove(topic, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _consumer.Unsubscribe();
        }

        return Task.CompletedTask;
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(ConsumeResult<string, byte[]> consumeResult)
    {
        var body = _serializer.Deserialize<T>(consumeResult.Message.Value);
        var headers = new Dictionary<string, string>();
        string? correlationId = null;
        var timestamp = DateTimeOffset.UtcNow;

        if (consumeResult.Message.Headers != null)
        {
            foreach (var header in consumeResult.Message.Headers)
            {
                var value = Encoding.UTF8.GetString(header.GetValueBytes());
                if (header.Key == "meshbus-correlation-id")
                {
                    correlationId = value;
                }
                else if (header.Key == "meshbus-timestamp")
                {
                    if (DateTimeOffset.TryParse(value, out var ts))
                        timestamp = ts;
                }
                else
                {
                    headers[header.Key] = value;
                }
            }
        }

        return new MeshBusMessage<T>
        {
            Id = consumeResult.Message.Key ?? Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Headers = headers,
            Body = body!,
            CorrelationId = correlationId,
            Topic = consumeResult.Topic
        };
    }

    private static AutoOffsetReset ParseAutoOffsetReset(string value) => value.ToLowerInvariant() switch
    {
        "earliest" => Confluent.Kafka.AutoOffsetReset.Earliest,
        "latest" => Confluent.Kafka.AutoOffsetReset.Latest,
        "error" => Confluent.Kafka.AutoOffsetReset.Error,
        _ => Confluent.Kafka.AutoOffsetReset.Earliest
    };

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            foreach (var kvp in _activeSubscriptions)
            {
                kvp.Value.Cancel();
                kvp.Value.Dispose();
            }
            _activeSubscriptions.Clear();
            _consumer.Close();
            _consumer.Dispose();
            _disposed = true;
        }
        return ValueTask.CompletedTask;
    }
}

