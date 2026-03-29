using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.GooglePubSub;

/// <summary>
/// Google Cloud Pub/Sub implementation of <see cref="IMeshBusPublisher"/>.
/// Publishes messages to Pub/Sub topics, optionally auto-creating topics when
/// <see cref="GooglePubSubOptions.AutoCreateResources"/> is enabled.
/// </summary>
public class GooglePubSubPublisher : IMeshBusPublisher
{
    private readonly PublisherServiceApiClient _publisherApi;
    private readonly IMessageSerializer _serializer;
    private readonly GooglePubSubOptions _options;
    private bool _disposed;

    /// <summary>Creates a new <see cref="GooglePubSubPublisher"/>.</summary>
    public GooglePubSubPublisher(
        PublisherServiceApiClient publisherApi,
        IMessageSerializer serializer,
        GooglePubSubOptions options)
    {
        _publisherApi = publisherApi ?? throw new ArgumentNullException(nameof(publisherApi));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var topicName = TopicName.FromProjectTopic(_options.ProjectId, message.Topic);

        if (_options.AutoCreateResources)
            await EnsureTopicExistsAsync(topicName, cancellationToken);

        try
        {
            var pubsubMessage = CreatePubSubMessage(message);
            await _publisherApi.PublishAsync(topicName, new[] { pubsubMessage });
        }
        catch (RpcException ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Status.Detail}",
                ex,
                "GooglePubSub");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var byTopic = messages.GroupBy(m => m.Topic);

        foreach (var group in byTopic)
        {
            var topicName = TopicName.FromProjectTopic(_options.ProjectId, group.Key);

            if (_options.AutoCreateResources)
                await EnsureTopicExistsAsync(topicName, cancellationToken);

            try
            {
                var pubsubMessages = group.Select(CreatePubSubMessage).ToList();
                await _publisherApi.PublishAsync(topicName, pubsubMessages);
            }
            catch (RpcException ex)
            {
                throw new MeshBusException(
                    $"Failed to publish batch to topic '{group.Key}': {ex.Status.Detail}",
                    ex,
                    "GooglePubSub");
            }
        }
    }

    private PubsubMessage CreatePubSubMessage<T>(MeshBusMessage<T> message)
    {
        var body = _serializer.Serialize(message.Body);
        var pubsubMessage = new PubsubMessage
        {
            Data = ByteString.CopyFrom(body)
        };

        pubsubMessage.Attributes["meshbus.id"] = message.Id;
        pubsubMessage.Attributes["meshbus.topic"] = message.Topic;
        pubsubMessage.Attributes["meshbus.timestamp"] = message.Timestamp.ToString("O");

        if (!string.IsNullOrEmpty(message.CorrelationId))
            pubsubMessage.Attributes["meshbus.correlationId"] = message.CorrelationId;

        foreach (var header in message.Headers)
            pubsubMessage.Attributes[$"meshbus.header.{header.Key}"] = header.Value;

        return pubsubMessage;
    }

    private async Task EnsureTopicExistsAsync(TopicName topicName, CancellationToken ct)
    {
        try
        {
            await _publisherApi.CreateTopicAsync(topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Topic already exists — expected in normal operation.
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
