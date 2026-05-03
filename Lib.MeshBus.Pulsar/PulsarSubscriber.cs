using System.Buffers;
using System.Collections.Concurrent;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Pulsar;

/// <summary>
/// Apache Pulsar implementation of <see cref="IMeshBusSubscriber"/>.
/// </summary>
public class PulsarSubscriber : IMeshBusSubscriber
{
    private readonly IPulsarClient _client;
    private readonly string _subscriptionName;
    private readonly string _subscriptionType;
    private readonly string _initialPosition;
    private readonly IMessageSerializer _serializer;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, Task> _consumeLoops = new();
    private readonly ConcurrentDictionary<string, IConsumer<ReadOnlySequence<byte>>> _consumers = new();
    private readonly bool _ownsClient;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="PulsarSubscriber"/> using the provided Pulsar client and serializer.
    /// </summary>
    public PulsarSubscriber(IPulsarClient client, IMessageSerializer serializer,
        string subscriptionName = "meshbus-subscription",
        string subscriptionType = "Shared",
        string initialPosition = "Earliest")
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _subscriptionName = subscriptionName;
        _subscriptionType = subscriptionType;
        _initialPosition = initialPosition;
        _ownsClient = false;
    }

    /// <summary>
    /// Creates a new <see cref="PulsarSubscriber"/> using <see cref="PulsarOptions"/>.
    /// </summary>
    public PulsarSubscriber(IOptions<PulsarOptions> options, IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        _client = PulsarClient.Builder()
            .ServiceUrl(new Uri(options.Value.ServiceUrl))
            .Build();

        _subscriptionName = options.Value.SubscriptionName;
        _subscriptionType = options.Value.SubscriptionType;
        _initialPosition = options.Value.InitialPosition;
        _ownsClient = true;
    }

    /// <inheritdoc />
    public Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_activeSubscriptions.ContainsKey(topic))
            throw new MeshBusException($"Already subscribed to topic '{topic}'.", new InvalidOperationException(), "Pulsar");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSubscriptions[topic] = cts;

        var loopTask = Task.Run(async () =>
        {
            IConsumer<ReadOnlySequence<byte>>? consumer = null;
            try
            {
            consumer = _client.CreateConsumer(new ConsumerOptions<ReadOnlySequence<byte>>(
                _subscriptionName,
                topic,
                Schema.ByteSequence)
            {
                SubscriptionType = ParseSubscriptionType(_subscriptionType),
                InitialPosition = ParseInitialPosition(_initialPosition)
            });

                _consumers[topic] = consumer;

                while (!cts.Token.IsCancellationRequested)
                {
                    IMessage<ReadOnlySequence<byte>> message;
                    try
                    {
                        message = await consumer.Receive(cts.Token);
                    }
                    catch (OperationCanceledException) { break; }

                    try
                    {
                        var meshBusMessage = ConvertToMeshBusMessage<T>(message, topic);
                        await handler(meshBusMessage);
                        await consumer.Acknowledge(message.MessageId, cts.Token);
                    }
                    catch (Exception) when (!cts.Token.IsCancellationRequested)
                    {
                        // Continue on handler errors to avoid breaking the loop
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on unsubscribe/dispose
            }
            catch (Exception ex) when (!cts.Token.IsCancellationRequested)
            {
                throw new MeshBusException(
                    $"Consumer loop for topic '{topic}' terminated unexpectedly: {ex.Message}",
                    ex,
                    "Pulsar");
            }
            finally
            {
                if (consumer is not null)
                    await consumer.DisposeAsync();

                _consumers.TryRemove(topic, out _);
            }
        }, cts.Token);

        _consumeLoops[topic] = loopTask;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_activeSubscriptions.TryRemove(topic, out var cts))
        {
            cts.Cancel();

            if (_consumeLoops.TryRemove(topic, out var loopTask))
            {
                try { await loopTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch (MeshBusException) { }
            }

            cts.Dispose();
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(IMessage<ReadOnlySequence<byte>> message, string topic)
    {
        var bytes = message.Data.ToArray();
        var body = _serializer.Deserialize<T>(bytes);
        var headers = new Dictionary<string, string>();
        string? correlationId = null;
        var timestamp = DateTimeOffset.UtcNow;
        var messageId = Guid.NewGuid().ToString();

        foreach (var (key, value) in message.Properties)
        {
            switch (key)
            {
                case "meshbus-correlation-id":
                    correlationId = value;
                    break;
                case "meshbus-timestamp":
                    if (DateTimeOffset.TryParse(value, out var ts))
                        timestamp = ts;
                    break;
                case "meshbus-message-id":
                    messageId = value;
                    break;
                default:
                    headers[key] = value;
                    break;
            }
        }

        return new MeshBusMessage<T>
        {
            Id = messageId,
            Timestamp = timestamp,
            Headers = headers,
            Body = body!,
            CorrelationId = correlationId,
            Topic = topic
        };
    }

    private static SubscriptionType ParseSubscriptionType(string value) => value.ToLowerInvariant() switch
    {
        "exclusive" => DotPulsar.SubscriptionType.Exclusive,
        "failover" => DotPulsar.SubscriptionType.Failover,
        "keyshared" or "key_shared" => DotPulsar.SubscriptionType.KeyShared,
        _ => DotPulsar.SubscriptionType.Shared
    };

    private static SubscriptionInitialPosition ParseInitialPosition(string value) => value.ToLowerInvariant() switch
    {
        "latest" => SubscriptionInitialPosition.Latest,
        _ => SubscriptionInitialPosition.Earliest
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _activeSubscriptions)
            kvp.Value.Cancel();

        var tasks = _consumeLoops.Values.Select(t => t.ContinueWith(_ => { }, TaskContinuationOptions.None));
        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var kvp in _activeSubscriptions)
            kvp.Value.Dispose();

        _activeSubscriptions.Clear();
        _consumeLoops.Clear();

        if (_ownsClient)
            await _client.DisposeAsync();
    }
}
