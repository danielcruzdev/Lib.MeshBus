using System.Collections.Concurrent;
using System.Text;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.EventHubs;

/// <summary>
/// Azure Event Hubs implementation of <see cref="IMeshBusPublisher"/>.
/// Creates one <see cref="EventHubProducerClient"/> per event hub (topic).
/// </summary>
public class EventHubsPublisher : IMeshBusPublisher
{
    private readonly Func<string, EventHubProducerClient> _clientFactory;
    private readonly IMessageSerializer _serializer;
    private readonly ConcurrentDictionary<string, EventHubProducerClient> _producers = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="EventHubsPublisher"/> using a namespace connection string.
    /// The connection string must not contain an EntityPath — the event hub name is
    /// taken from <see cref="MeshBusMessage{T}.Topic"/> at publish time.
    /// </summary>
    public EventHubsPublisher(string connectionString, IMessageSerializer serializer)
        : this(name => new EventHubProducerClient(connectionString, name), serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
    }

    /// <summary>
    /// Creates a new <see cref="EventHubsPublisher"/> using a custom client factory.
    /// Intended for unit testing.
    /// </summary>
    public EventHubsPublisher(Func<string, EventHubProducerClient> clientFactory, IMessageSerializer serializer)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var producer = GetOrCreateProducer(message.Topic);
            var eventData = CreateEventData(message);
            await producer.SendAsync(new[] { eventData }, cancellationToken);
        }
        catch (EventHubsException ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to event hub '{message.Topic}': {ex.Message}",
                ex,
                "EventHubs");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var byTopic = messages.GroupBy(m => m.Topic);

        foreach (var group in byTopic)
        {
            try
            {
                var producer = GetOrCreateProducer(group.Key);
                var batch = await producer.CreateBatchAsync(cancellationToken);

                foreach (var message in group)
                {
                    var eventData = CreateEventData(message);
                    if (!batch.TryAdd(eventData))
                    {
                        await producer.SendAsync(batch, cancellationToken);
                        batch = await producer.CreateBatchAsync(cancellationToken);
                        if (!batch.TryAdd(eventData))
                        {
                            throw new MeshBusException(
                                $"Failed to add message to batch for event hub '{group.Key}' because it exceeds the maximum allowed size.",
                                null,
                                "EventHubs");
                        }
                    }
                }

                if (batch.Count > 0)
                    await producer.SendAsync(batch, cancellationToken);
            }
            catch (EventHubsException ex)
            {
                throw new MeshBusException(
                    $"Failed to publish batch to event hub '{group.Key}': {ex.Message}",
                    ex,
                    "EventHubs");
            }
        }
    }

    private EventHubProducerClient GetOrCreateProducer(string eventHubName) =>
        _producers.GetOrAdd(eventHubName, name => _clientFactory(name));

    private EventData CreateEventData<T>(MeshBusMessage<T> message)
    {
        var body = _serializer.Serialize(message.Body);
        var eventData = new EventData(body);

        eventData.Properties["meshbus.id"] = message.Id;
        eventData.Properties["meshbus.topic"] = message.Topic;
        eventData.Properties["meshbus.timestamp"] = message.Timestamp.ToString("O");

        if (!string.IsNullOrEmpty(message.CorrelationId))
            eventData.Properties["meshbus.correlationId"] = message.CorrelationId;

        foreach (var header in message.Headers)
            eventData.Properties[$"meshbus.header.{header.Key}"] = header.Value;

        return eventData;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var producer in _producers.Values)
            await producer.DisposeAsync();

        _producers.Clear();
    }
}
