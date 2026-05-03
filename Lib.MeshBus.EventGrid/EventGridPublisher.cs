using System.Text.Json;
using Azure;
using Azure.Messaging.EventGrid;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.EventGrid;

/// <summary>
/// Azure Event Grid implementation of <see cref="IMeshBusPublisher"/>.
/// Publishes events to an Event Grid topic using the EventGridPublisherClient.
/// </summary>
public class EventGridPublisher : IMeshBusPublisher
{
    private readonly EventGridPublisherClient _client;
    private readonly IMessageSerializer _serializer;
    private bool _disposed;

    /// <summary>Creates a new <see cref="EventGridPublisher"/>.</summary>
    public EventGridPublisher(EventGridPublisherClient client, IMessageSerializer serializer)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var eventGridEvent = CreateEvent(message);
            await _client.SendEventAsync(eventGridEvent, cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            throw new MeshBusException(
                $"Failed to publish event to topic '{message.Topic}': {ex.Message}",
                ex,
                "EventGrid");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messages);

        var byTopic = messages.GroupBy(m => m.Topic);

        foreach (var group in byTopic)
        {
            try
            {
                var events = group.Select(CreateEvent).ToList();
                await _client.SendEventsAsync(events, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                throw new MeshBusException(
                    $"Failed to publish batch to topic '{group.Key}': {ex.Message}",
                    ex,
                    "EventGrid");
            }
        }
    }

    private EventGridEvent CreateEvent<T>(MeshBusMessage<T> message)
    {
        var bodyBytes = _serializer.Serialize(message.Body);
        var bodyBase64 = Convert.ToBase64String(bodyBytes);

        var data = new Dictionary<string, object>
        {
            ["body"] = bodyBase64,
            ["meshbus.timestamp"] = message.Timestamp.ToString("O"),
        };

        if (!string.IsNullOrEmpty(message.CorrelationId))
            data["meshbus.correlationId"] = message.CorrelationId;

        foreach (var header in message.Headers)
            data[$"meshbus.header.{header.Key}"] = header.Value;

        return new EventGridEvent(
            subject: message.Topic,
            eventType: "MeshBus.Message",
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(data))
        {
            Id = message.Id,
            EventTime = message.Timestamp
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
