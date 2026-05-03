using System.Collections.Concurrent;
using System.Text.Json;
using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.EventGrid;

/// <summary>
/// Azure Event Grid implementation of <see cref="IMeshBusSubscriber"/>.
/// Uses pull delivery via <see cref="EventGridReceiverClient"/> to receive events
/// from Event Grid namespace topic subscriptions.
/// </summary>
public class EventGridSubscriber : IMeshBusSubscriber
{
    private readonly EventGridReceiverClient _receiverClient;
    private readonly IMessageSerializer _serializer;
    private readonly EventGridOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, Task> _consumeLoops = new();
    private bool _disposed;

    /// <summary>Creates a new <see cref="EventGridSubscriber"/>.</summary>
    public EventGridSubscriber(
        EventGridReceiverClient receiverClient,
        IMessageSerializer serializer,
        EventGridOptions options)
    {
        _receiverClient = receiverClient ?? throw new ArgumentNullException(nameof(receiverClient));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_activeSubscriptions.ContainsKey(topic))
            throw new MeshBusException(
                $"Already subscribed to topic '{topic}'.",
                new InvalidOperationException(),
                "EventGrid");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSubscriptions[topic] = cts;

        var loopTask = Task.Run(async () => await ConsumeLoopAsync(topic, handler, cts.Token));
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
                try { await loopTask.ConfigureAwait(false); } catch { }
            }

            cts.Dispose();
        }
    }

    private async Task ConsumeLoopAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ReceiveResult result;

            try
            {
                result = await _receiverClient.ReceiveAsync(
                    maxEvents: _options.MaxEvents,
                    maxWaitTime: TimeSpan.FromSeconds(_options.MaxWaitTimeSeconds),
                    cancellationToken: ct);
            }
            catch (OperationCanceledException) { break; }
            catch (RequestFailedException ex) when (ct.IsCancellationRequested)
            {
                _ = ex;
                break;
            }
            catch (RequestFailedException ex)
            {
                throw new MeshBusException(
                    $"Error receiving events for topic '{topic}': {ex.Message}",
                    ex,
                    "EventGrid");
            }

            if (result.Details.Count == 0)
            {
                await Task.Delay(_options.EmptyPollDelay ?? TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
                continue;
            }

            var lockTokensToAcknowledge = new List<string>();

            foreach (var detail in result.Details)
            {
                try
                {
                    var meshMessage = ConvertToMeshBusMessage<T>(detail.Event, topic);
                    await handler(meshMessage);
                    lockTokensToAcknowledge.Add(detail.BrokerProperties.LockToken);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch
                {
                    // Handler errors: do not ack — event will be redelivered.
                }
            }

            if (lockTokensToAcknowledge.Count > 0)
            {
                try
                {
                    await _receiverClient.AcknowledgeAsync(lockTokensToAcknowledge, ct);
                }
                catch
                {
                    // Acknowledge failures are non-fatal.
                }
            }
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(CloudEvent cloudEvent, string fallbackTopic)
    {
        var data = cloudEvent.Data?.ToObjectFromJson<Dictionary<string, JsonElement>>();

        byte[] bodyBytes;
        if (data is not null && data.TryGetValue("body", out var bodyElement))
        {
            bodyBytes = Convert.FromBase64String(bodyElement.GetString()!);
        }
        else if (cloudEvent.Data is not null)
        {
            bodyBytes = cloudEvent.Data.ToArray();
        }
        else
        {
            bodyBytes = [];
        }

        var body = _serializer.Deserialize<T>(bodyBytes);

        var message = new MeshBusMessage<T>
        {
            Id = cloudEvent.Id ?? Guid.NewGuid().ToString(),
            Topic = cloudEvent.Subject ?? fallbackTopic,
            Body = body!,
            Timestamp = cloudEvent.Time ?? DateTimeOffset.UtcNow,
        };

        if (data is not null)
        {
            if (data.TryGetValue("meshbus.timestamp", out var tsVal)
                && DateTimeOffset.TryParse(tsVal.GetString(), out var dto))
            {
                message.Timestamp = dto;
            }

            if (data.TryGetValue("meshbus.correlationId", out var corrVal))
            {
                message.CorrelationId = corrVal.GetString();
            }

            foreach (var kvp in data)
            {
                if (kvp.Key.StartsWith("meshbus.header.", StringComparison.Ordinal))
                    message.Headers[kvp.Key["meshbus.header.".Length..]] = kvp.Value.GetString()!;
            }
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

        foreach (var loop in _consumeLoops.Values)
        {
            try { await loop.ConfigureAwait(false); } catch { }
        }
    }
}
