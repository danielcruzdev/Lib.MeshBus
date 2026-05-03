using System.Collections.Concurrent;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.EventHubs;

/// <summary>
/// Azure Event Hubs implementation of <see cref="IMeshBusSubscriber"/>.
/// Uses <see cref="EventHubConsumerClient.ReadEventsAsync(System.Threading.CancellationToken)"/> to stream events
/// from all partitions of the target event hub.
/// </summary>
public class EventHubsSubscriber : IMeshBusSubscriber
{
    private readonly Func<string, EventHubConsumerClient> _clientFactory;
    private readonly IMessageSerializer _serializer;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, (Task Loop, EventHubConsumerClient Client)> _consumeLoops = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="EventHubsSubscriber"/> using a namespace connection string.
    /// </summary>
    public EventHubsSubscriber(string connectionString, string consumerGroup, IMessageSerializer serializer)
        : this(name => new EventHubConsumerClient(consumerGroup, connectionString, name), serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
    }

    /// <summary>
    /// Creates a new <see cref="EventHubsSubscriber"/> using a custom client factory.
    /// Intended for unit testing.
    /// </summary>
    public EventHubsSubscriber(Func<string, EventHubConsumerClient> clientFactory, IMessageSerializer serializer)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc />
    public Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_activeSubscriptions.ContainsKey(topic))
            throw new MeshBusException(
                $"Already subscribed to event hub '{topic}'.",
                new InvalidOperationException(),
                "EventHubs");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSubscriptions[topic] = cts;

        var consumer = _clientFactory(topic);

        var loopTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var partitionEvent in consumer.ReadEventsAsync(cts.Token))
                {
                    if (partitionEvent.Data == null) continue;

                    try
                    {
                        var meshMessage = ConvertToMeshBusMessage<T>(partitionEvent.Data, topic);
                        await handler(meshMessage);
                    }
                    catch
                    {
                        if (cts.Token.IsCancellationRequested) break;
                    }
                }
            }
            catch (OperationCanceledException) { }
        });

        _consumeLoops[topic] = (loopTask, consumer);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_activeSubscriptions.TryRemove(topic, out var cts))
        {
            cts.Cancel();

            if (_consumeLoops.TryRemove(topic, out var entry))
            {
                try { await entry.Loop.ConfigureAwait(false); } catch { }
                await entry.Client.DisposeAsync();
            }

            cts.Dispose();
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(EventData eventData, string topic)
    {
        var body = _serializer.Deserialize<T>(eventData.Body.ToArray());

        var id = eventData.Properties.TryGetValue("meshbus.id", out var idObj)
            ? idObj?.ToString() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        var correlationId = eventData.Properties.TryGetValue("meshbus.correlationId", out var corrObj)
            ? corrObj?.ToString()
            : null;

        var timestamp = eventData.Properties.TryGetValue("meshbus.timestamp", out var tsObj)
            && tsObj?.ToString() is string tsStr
            && DateTimeOffset.TryParse(tsStr, out var dto)
            ? dto
            : DateTimeOffset.UtcNow;

        var storedTopic = eventData.Properties.TryGetValue("meshbus.topic", out var topicObj)
            ? topicObj?.ToString() ?? topic
            : topic;

        var message = new MeshBusMessage<T>
        {
            Id = id,
            Topic = storedTopic,
            Body = body!,
            Timestamp = timestamp,
            CorrelationId = correlationId
        };

        foreach (var prop in eventData.Properties)
        {
            if (prop.Key.StartsWith("meshbus.header.", StringComparison.Ordinal))
                message.Headers[prop.Key["meshbus.header.".Length..]] = prop.Value?.ToString() ?? string.Empty;
        }

        return message;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var cts in _activeSubscriptions.Values)
            cts.Cancel();

        foreach (var (loop, client) in _consumeLoops.Values)
        {
            try { await loop.ConfigureAwait(false); } catch { }
            await client.DisposeAsync();
        }
    }
}
