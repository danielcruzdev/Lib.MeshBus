using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.Sqs;

/// <summary>
/// AWS SQS implementation of <see cref="IMeshBusPublisher"/>.
/// The message body is a JSON envelope that wraps the serialized payload together
/// with MeshBus metadata so that no attribute-count limits are hit.
/// </summary>
public class SqsPublisher : IMeshBusPublisher
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IMessageSerializer _serializer;
    private readonly SqsQueueResolver _resolver;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="SqsPublisher"/>.
    /// </summary>
    public SqsPublisher(IAmazonSQS sqsClient, IMessageSerializer serializer, SqsQueueResolver resolver)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var queueUrl = await _resolver.GetOrCreateQueueUrlAsync(message.Topic, cancellationToken);
            var body = BuildEnvelope(message);
            await _sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = body
            }, cancellationToken);
        }
        catch (AmazonSQSException ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to queue '{message.Topic}': {ex.Message}",
                ex,
                "SQS");
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
                var queueUrl = await _resolver.GetOrCreateQueueUrlAsync(group.Key, cancellationToken);
                // SQS batch API allows up to 10 messages per request
                var batch = group
                    .Select((msg, idx) => new SendMessageBatchRequestEntry
                    {
                        Id = idx.ToString(),
                        MessageBody = BuildEnvelope(msg)
                    })
                    .ToList();

                for (int i = 0; i < batch.Count; i += 10)
                {
                    var chunk = batch.Skip(i).Take(10).ToList();
                    var response = await _sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
                    {
                        QueueUrl = queueUrl,
                        Entries = chunk
                    }, cancellationToken);

                    if (response.Failed.Count > 0)
                    {
                        throw new MeshBusException(
                            $"Failed to publish {response.Failed.Count} message(s) in batch to queue '{group.Key}'.",
                            new AggregateException(response.Failed.Select(f => new Exception(f.Message))),
                            "SQS");
                    }
                }
            }
            catch (MeshBusException) { throw; }
            catch (AmazonSQSException ex)
            {
                throw new MeshBusException(
                    $"Failed to publish batch to queue '{group.Key}': {ex.Message}",
                    ex,
                    "SQS");
            }
        }
    }

    private string BuildEnvelope<T>(MeshBusMessage<T> message)
    {
        var bodyBytes = _serializer.Serialize(message.Body);
        var envelope = new SqsMessageEnvelope
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
            _sqsClient.Dispose();
        }
        return ValueTask.CompletedTask;
    }
}
