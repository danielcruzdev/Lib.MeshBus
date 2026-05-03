using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.EventBridge;

/// <summary>
/// AWS EventBridge implementation of <see cref="IMeshBusPublisher"/>.
/// Publishes events to an EventBridge event bus using PutEvents.
/// The message topic is mapped to the EventBridge detail-type.
/// </summary>
public class EventBridgePublisher : IMeshBusPublisher
{
    private readonly IAmazonEventBridge _client;
    private readonly IMessageSerializer _serializer;
    private readonly EventBridgeOptions _options;
    private bool _disposed;

    /// <summary>Creates a new <see cref="EventBridgePublisher"/>.</summary>
    public EventBridgePublisher(
        IAmazonEventBridge client,
        IMessageSerializer serializer,
        EventBridgeOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var entry = CreateEntry(message);
            var response = await _client.PutEventsAsync(new PutEventsRequest
            {
                Entries = [entry]
            }, cancellationToken);

            if (response.FailedEntryCount > 0)
            {
                var failed = response.Entries.First(e => !string.IsNullOrEmpty(e.ErrorCode));
                throw new MeshBusException(
                    $"Failed to publish event to bus '{_options.EventBusName}' for topic '{message.Topic}': {failed.ErrorMessage}",
                    new Exception(failed.ErrorCode),
                    "EventBridge");
            }
        }
        catch (MeshBusException) { throw; }
        catch (AmazonEventBridgeException ex)
        {
            throw new MeshBusException(
                $"Failed to publish event to bus '{_options.EventBusName}' for topic '{message.Topic}': {ex.Message}",
                ex,
                "EventBridge");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var entries = messages.Select(CreateEntry).ToList();

        // EventBridge PutEvents supports up to 10 entries per call.
        for (int i = 0; i < entries.Count; i += 10)
        {
            var chunk = entries.Skip(i).Take(10).ToList();

            try
            {
                var response = await _client.PutEventsAsync(new PutEventsRequest
                {
                    Entries = chunk
                }, cancellationToken);

                if (response.FailedEntryCount > 0)
                {
                    throw new MeshBusException(
                        $"Failed to publish {response.FailedEntryCount} event(s) in batch to bus '{_options.EventBusName}'.",
                        new AggregateException(
                            response.Entries
                                .Where(e => !string.IsNullOrEmpty(e.ErrorCode))
                                .Select(e => new Exception(e.ErrorMessage))),
                        "EventBridge");
                }
            }
            catch (MeshBusException) { throw; }
            catch (AmazonEventBridgeException ex)
            {
                throw new MeshBusException(
                    $"Failed to publish batch to bus '{_options.EventBusName}': {ex.Message}",
                    ex,
                    "EventBridge");
            }
        }
    }

    private PutEventsRequestEntry CreateEntry<T>(MeshBusMessage<T> message)
    {
        var bodyBytes = _serializer.Serialize(message.Body);
        var envelope = new EventBridgeMessageEnvelope
        {
            Id = message.Id,
            Topic = message.Topic,
            Timestamp = message.Timestamp,
            CorrelationId = message.CorrelationId,
            Headers = message.Headers,
            Body = Convert.ToBase64String(bodyBytes)
        };

        return new PutEventsRequestEntry
        {
            EventBusName = _options.EventBusName,
            Source = _options.Source,
            DetailType = message.Topic,
            Detail = JsonSerializer.Serialize(envelope),
            Time = message.Timestamp.UtcDateTime
        };
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _client.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
