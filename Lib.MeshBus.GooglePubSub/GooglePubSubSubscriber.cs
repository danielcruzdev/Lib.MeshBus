using System.Collections.Concurrent;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.GooglePubSub;

/// <summary>
/// Google Cloud Pub/Sub implementation of <see cref="IMeshBusSubscriber"/>.
/// Uses a polling loop via <see cref="SubscriberServiceApiClient"/> (<c>PullAsync</c>) so that
/// the provider integrates cleanly with the MeshBus abstraction without requiring a
/// background gRPC streaming thread that is hard to cancel.
/// </summary>
public class GooglePubSubSubscriber : IMeshBusSubscriber
{
    private readonly SubscriberServiceApiClient _subscriberApi;
    private readonly PublisherServiceApiClient _publisherApi;
    private readonly IMessageSerializer _serializer;
    private readonly GooglePubSubOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, Task> _consumeLoops = new();
    private bool _disposed;

    /// <summary>Creates a new <see cref="GooglePubSubSubscriber"/>.</summary>
    public GooglePubSubSubscriber(
        SubscriberServiceApiClient subscriberApi,
        PublisherServiceApiClient publisherApi,
        IMessageSerializer serializer,
        GooglePubSubOptions options)
    {
        _subscriberApi = subscriberApi ?? throw new ArgumentNullException(nameof(subscriberApi));
        _publisherApi = publisherApi ?? throw new ArgumentNullException(nameof(publisherApi));
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
                "GooglePubSub");

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
        var subscriptionName = SubscriptionName.FromProjectSubscription(
            _options.ProjectId,
            $"{topic}-{_options.SubscriptionSuffix}");

        if (_options.AutoCreateResources)
            await EnsureSubscriptionExistsAsync(topic, subscriptionName, ct);

        while (!ct.IsCancellationRequested)
        {
            PullResponse response;

            try
            {
                response = await _subscriberApi.PullAsync(
                    subscriptionName,
                    maxMessages: _options.MaxMessages);
            }
            catch (OperationCanceledException) { break; }
            catch (RpcException ex) when (ct.IsCancellationRequested)
            {
                _ = ex;
                break;
            }
            catch (RpcException ex)
            {
                throw new MeshBusException(
                    $"Error pulling messages from subscription for topic '{topic}': {ex.Status.Detail}",
                    ex,
                    "GooglePubSub");
            }

            if (response.ReceivedMessages.Count == 0)
            {
                // No messages — back-off briefly to avoid a tight polling loop.
                await Task.Delay(_options.EmptyPollDelay ?? TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
                continue;
            }

            var ackIds = new List<string>(response.ReceivedMessages.Count);

            foreach (var received in response.ReceivedMessages)
            {
                try
                {
                    var meshMessage = ConvertToMeshBusMessage<T>(received.Message, topic);
                    await handler(meshMessage);
                    ackIds.Add(received.AckId);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch
                {
                    // Handler errors: do not ack — message will be redelivered.
                }
            }

            if (ackIds.Count > 0)
            {
                try
                {
                    await _subscriberApi.AcknowledgeAsync(subscriptionName, ackIds);
                }
                catch
                {
                    // Ack failures are non-fatal; messages will be redelivered.
                }
            }
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(PubsubMessage pubsubMessage, string fallbackTopic)
    {
        var body = _serializer.Deserialize<T>(pubsubMessage.Data.ToByteArray());

        var id = pubsubMessage.Attributes.TryGetValue("meshbus.id", out var idVal)
            ? idVal
            : pubsubMessage.MessageId;

        var storedTopic = pubsubMessage.Attributes.TryGetValue("meshbus.topic", out var topicVal)
            ? topicVal
            : fallbackTopic;

        var correlationId = pubsubMessage.Attributes.TryGetValue("meshbus.correlationId", out var corrVal)
            ? corrVal
            : null;

        var timestamp = pubsubMessage.Attributes.TryGetValue("meshbus.timestamp", out var tsVal)
            && DateTimeOffset.TryParse(tsVal, out var dto)
            ? dto
            : DateTimeOffset.UtcNow;

        var message = new MeshBusMessage<T>
        {
            Id = id,
            Topic = storedTopic,
            Body = body!,
            Timestamp = timestamp,
            CorrelationId = correlationId
        };

        foreach (var attr in pubsubMessage.Attributes)
        {
            if (attr.Key.StartsWith("meshbus.header.", StringComparison.Ordinal))
                message.Headers[attr.Key["meshbus.header.".Length..]] = attr.Value;
        }

        return message;
    }

    private async Task EnsureSubscriptionExistsAsync(string topic, SubscriptionName subscriptionName, CancellationToken ct)
    {
        var topicName = TopicName.FromProjectTopic(_options.ProjectId, topic);

        // Ensure topic exists first
        try
        {
            await _publisherApi.CreateTopicAsync(topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists) { }

        // Create subscription
        try
        {
            await _subscriberApi.CreateSubscriptionAsync(
                subscriptionName,
                topicName,
                pushConfig: null,
                ackDeadlineSeconds: _options.AckDeadlineSeconds);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists) { }
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
