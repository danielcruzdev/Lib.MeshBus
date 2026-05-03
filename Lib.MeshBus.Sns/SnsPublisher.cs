using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.Sns;

/// <summary>
/// AWS SNS implementation of <see cref="IMeshBusPublisher"/>.
/// The message body is a JSON envelope that wraps the serialized payload together
/// with MeshBus metadata.
/// </summary>
public class SnsPublisher : IMeshBusPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IMessageSerializer _serializer;
    private readonly SnsTopicResolver _resolver;
    private bool _disposed;

    /// <summary>Creates a new <see cref="SnsPublisher"/>.</summary>
    public SnsPublisher(
        IAmazonSimpleNotificationService snsClient,
        IMessageSerializer serializer,
        SnsTopicResolver resolver)
    {
        _snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var topicArn = await _resolver.GetOrCreateTopicArnAsync(message.Topic, cancellationToken);
            var body = BuildEnvelope(message);

            await _snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = topicArn,
                Message = body
            }, cancellationToken);
        }
        catch (MeshBusException) { throw; }
        catch (AmazonSimpleNotificationServiceException ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Message}",
                ex,
                "SNS");
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
                var topicArn = await _resolver.GetOrCreateTopicArnAsync(group.Key, cancellationToken);

                // SNS PublishBatch supports up to 10 messages per request.
                var entries = group
                    .Select((msg, idx) => new PublishBatchRequestEntry
                    {
                        Id = idx.ToString(),
                        Message = BuildEnvelope(msg)
                    })
                    .ToList();

                for (int i = 0; i < entries.Count; i += 10)
                {
                    var chunk = entries.Skip(i).Take(10).ToList();
                    var response = await _snsClient.PublishBatchAsync(new PublishBatchRequest
                    {
                        TopicArn = topicArn,
                        PublishBatchRequestEntries = chunk
                    }, cancellationToken);

                    if (response.Failed.Count > 0)
                    {
                        throw new MeshBusException(
                            $"Failed to publish {response.Failed.Count} message(s) in batch to topic '{group.Key}'.",
                            new AggregateException(response.Failed.Select(f => new Exception(f.Message))),
                            "SNS");
                    }
                }
            }
            catch (MeshBusException) { throw; }
            catch (AmazonSimpleNotificationServiceException ex)
            {
                throw new MeshBusException(
                    $"Failed to publish batch to topic '{group.Key}': {ex.Message}",
                    ex,
                    "SNS");
            }
        }
    }

    private string BuildEnvelope<T>(MeshBusMessage<T> message)
    {
        var bodyBytes = _serializer.Serialize(message.Body);
        var envelope = new SnsMessageEnvelope
        {
            Id = message.Id,
            Topic = message.Topic,
            Timestamp = message.Timestamp,
            CorrelationId = message.CorrelationId,
            Headers = message.Headers,
            Body = Convert.ToBase64String(bodyBytes)
        };
        return JsonSerializer.Serialize(envelope);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _snsClient.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
