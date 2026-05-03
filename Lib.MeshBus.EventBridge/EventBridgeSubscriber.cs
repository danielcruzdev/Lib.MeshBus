using System.Collections.Concurrent;
using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.EventBridge;

/// <summary>
/// AWS EventBridge implementation of <see cref="IMeshBusSubscriber"/>.
/// EventBridge is a push-based service, so this subscriber creates an SQS queue
/// with an EventBridge rule targeting it for pull-based consumption.
/// The rule matches events by detail-type (mapped from the MeshBus topic name).
/// </summary>
public class EventBridgeSubscriber : IMeshBusSubscriber
{
    private readonly IAmazonEventBridge _ebClient;
    private readonly IAmazonSQS _sqsClient;
    private readonly IMessageSerializer _serializer;
    private readonly EventBridgeOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, Task> _consumeLoops = new();
    private bool _disposed;

    /// <summary>Creates a new <see cref="EventBridgeSubscriber"/>.</summary>
    public EventBridgeSubscriber(
        IAmazonEventBridge ebClient,
        IAmazonSQS sqsClient,
        IMessageSerializer serializer,
        EventBridgeOptions options)
    {
        _ebClient = ebClient ?? throw new ArgumentNullException(nameof(ebClient));
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
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
                "EventBridge");

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
            queueUrl = await EnsureSqsTargetAsync(topic, ct);
        }
        catch (OperationCanceledException) { return; }
        catch (MeshBusException) { throw; }
        catch (Exception ex)
        {
            throw new MeshBusException(
                $"Failed to set up SQS target for EventBridge topic '{topic}': {ex.Message}",
                ex,
                "EventBridge");
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
            catch (AmazonSQSException ex)
            {
                if (ct.IsCancellationRequested) break;
                throw new MeshBusException(
                    $"Error receiving messages from SQS queue for EventBridge topic '{topic}': {ex.Message}",
                    ex,
                    "EventBridge");
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
        // EventBridge delivers the event wrapped in an EventBridge envelope.
        // The "detail" field contains our MeshBus envelope.
        try
        {
            using var doc = JsonDocument.Parse(sqsBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("detail", out var detailProp))
            {
                var detail = detailProp.GetRawText();
                return EventBridgeMessageEnvelope.ToMeshBusMessage<T>(detail, _serializer);
            }
        }
        catch (JsonException)
        {
            // Not JSON or unexpected structure — try raw.
        }

        // Fallback: treat the body as a direct MeshBus envelope.
        return EventBridgeMessageEnvelope.ToMeshBusMessage<T>(sqsBody, _serializer);
    }

    private async Task<string> EnsureSqsTargetAsync(string topic, CancellationToken ct)
    {
        var queueName = $"{topic}-meshbus-eb-sub";
        var ruleName = $"meshbus-{topic}";

        // Create or get the SQS queue.
        var createQueueResponse = await _sqsClient.CreateQueueAsync(
            new CreateQueueRequest { QueueName = queueName }, ct);
        var queueUrl = createQueueResponse.QueueUrl;

        if (_options.AutoCreateSqsTarget)
        {
            // Get the queue ARN.
            var attrResponse = await _sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,
                    AttributeNames = ["QueueArn"]
                }, ct);
            var queueArn = attrResponse.Attributes["QueueArn"];

            // Create an EventBridge rule that matches events with the given detail-type.
            var eventPattern = JsonSerializer.Serialize(new
            {
                source = new[] { _options.Source },
                detail_type = new[] { topic }
            });

            await _ebClient.PutRuleAsync(new PutRuleRequest
            {
                Name = ruleName,
                EventBusName = _options.EventBusName,
                EventPattern = eventPattern,
                State = RuleState.ENABLED
            }, ct);

            // Set the SQS queue as the target.
            await _ebClient.PutTargetsAsync(new PutTargetsRequest
            {
                Rule = ruleName,
                EventBusName = _options.EventBusName,
                Targets =
                [
                    new Target
                    {
                        Id = $"meshbus-sqs-{topic}",
                        Arn = queueArn
                    }
                ]
            }, ct);

            // Set queue policy to allow EventBridge to send messages.
            var policy = $$"""
            {
              "Version": "2012-10-17",
              "Statement": [{
                "Effect": "Allow",
                "Principal": {"Service": "events.amazonaws.com"},
                "Action": "sqs:SendMessage",
                "Resource": "{{queueArn}}"
              }]
            }
            """;

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

        _ebClient.Dispose();
        _sqsClient.Dispose();
    }
}
