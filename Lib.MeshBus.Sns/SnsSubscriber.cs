using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.Sns;

/// <summary>
/// AWS SNS implementation of <see cref="IMeshBusSubscriber"/>.
/// SNS is a push-based service, so this subscriber uses an SQS queue subscribed to the
/// SNS topic for pull-based delivery. The SQS queue can be auto-created and auto-subscribed
/// when <see cref="SnsOptions.AutoCreateSqsSubscription"/> is enabled.
/// </summary>
public class SnsSubscriber : IMeshBusSubscriber
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IAmazonSQS _sqsClient;
    private readonly IMessageSerializer _serializer;
    private readonly SnsTopicResolver _resolver;
    private readonly SnsOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, Task> _consumeLoops = new();
    private bool _disposed;

    /// <summary>Creates a new <see cref="SnsSubscriber"/>.</summary>
    public SnsSubscriber(
        IAmazonSimpleNotificationService snsClient,
        IAmazonSQS sqsClient,
        IMessageSerializer serializer,
        SnsTopicResolver resolver,
        SnsOptions options)
    {
        _snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
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
                "SNS");

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
        string queueUrl;

        try
        {
            var topicArn = await _resolver.GetOrCreateTopicArnAsync(topic, ct);
            queueUrl = await EnsureSqsQueueSubscribedAsync(topic, topicArn, ct);
        }
        catch (OperationCanceledException) { return; }
        catch (MeshBusException) { throw; }
        catch (Exception ex)
        {
            throw new MeshBusException(
                $"Failed to set up SQS subscription for SNS topic '{topic}': {ex.Message}",
                ex,
                "SNS");
        }

        var receiveRequest = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            MaxNumberOfMessages = Math.Min(_options.MaxNumberOfMessages, 10),
            WaitTimeSeconds = Math.Clamp(_options.WaitTimeSeconds, 0, 20)
        };

        while (!ct.IsCancellationRequested)
        {
            ReceiveMessageResponse response;

            try
            {
                response = await _sqsClient.ReceiveMessageAsync(receiveRequest, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Amazon.SQS.AmazonSQSException ex)
            {
                if (ct.IsCancellationRequested) break;
                throw new MeshBusException(
                    $"Error receiving messages from SQS queue for SNS topic '{topic}': {ex.Message}",
                    ex,
                    "SNS");
            }

            foreach (var sqsMessage in response.Messages)
            {
                try
                {
                    var meshMessage = ExtractMeshBusMessage<T>(sqsMessage.Body);
                    await handler(meshMessage);

                    await _sqsClient.DeleteMessageAsync(queueUrl, sqsMessage.ReceiptHandle, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch
                {
                    // Handler errors: don't delete — message redelivered via visibility timeout.
                }
            }
        }
    }

    private MeshBusMessage<T> ExtractMeshBusMessage<T>(string sqsBody)
    {
        // SNS wraps the original message in an SNS notification envelope.
        // Try to unwrap it first.
        try
        {
            using var doc = JsonDocument.Parse(sqsBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("Message", out var snsMessageProp))
            {
                // This is an SNS notification envelope — extract the inner message.
                var innerMessage = snsMessageProp.GetString()!;
                return SnsMessageEnvelope.ToMeshBusMessage<T>(innerMessage, _serializer);
            }
        }
        catch (JsonException)
        {
            // Not JSON or unexpected structure — try raw.
        }

        // Fallback: treat the body as a direct MeshBus envelope.
        return SnsMessageEnvelope.ToMeshBusMessage<T>(sqsBody, _serializer);
    }

    private async Task<string> EnsureSqsQueueSubscribedAsync(string topic, string topicArn, CancellationToken ct)
    {
        var queueName = $"{topic}-meshbus-sns-sub";

        // Create or get the SQS queue.
        var createQueueResponse = await _sqsClient.CreateQueueAsync(
            new CreateQueueRequest { QueueName = queueName }, ct);
        var queueUrl = createQueueResponse.QueueUrl;

        if (_options.AutoCreateSqsSubscription)
        {
            // Get the queue ARN.
            var attrResponse = await _sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,
                    AttributeNames = ["QueueArn"]
                }, ct);
            var queueArn = attrResponse.Attributes["QueueArn"];

            // Subscribe the SQS queue to the SNS topic.
            await _snsClient.SubscribeAsync(topicArn, "sqs", queueArn, ct);

            // Set queue policy to allow SNS to send messages.
            var policy = "{" +
                "\"Version\":\"2012-10-17\"," +
                "\"Statement\":[{" +
                    "\"Effect\":\"Allow\"," +
                    "\"Principal\":{\"Service\":\"sns.amazonaws.com\"}," +
                    "\"Action\":\"sqs:SendMessage\"," +
                    $"\"Resource\":\"{queueArn}\"," +
                    "\"Condition\":{\"ArnEquals\":{" +
                        $"\"aws:SourceArn\":\"{topicArn}\"" +
                    "}}" +
                "}]" +
            "}";

            await _sqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                Attributes = new Dictionary<string, string> { ["Policy"] = policy }
            }, ct);
        }

        return queueUrl;
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

        _snsClient.Dispose();
        _sqsClient.Dispose();
    }
}
