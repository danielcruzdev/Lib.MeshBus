using System.Collections.Concurrent;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.Sqs;

/// <summary>
/// AWS SQS implementation of <see cref="IMeshBusSubscriber"/>.
/// Runs a long-polling loop per topic (queue), deserializing the MeshBus JSON envelope
/// from the message body and forwarding it to the registered handler.
/// </summary>
public class SqsSubscriber : IMeshBusSubscriber
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IMessageSerializer _serializer;
    private readonly SqsQueueResolver _resolver;
    private readonly SqsOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, Task> _consumeLoops = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="SqsSubscriber"/>.
    /// </summary>
    public SqsSubscriber(IAmazonSQS sqsClient, IMessageSerializer serializer, SqsQueueResolver resolver, SqsOptions options)
    {
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
                $"Already subscribed to queue '{topic}'.",
                new InvalidOperationException(),
                "SQS");

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
        string? queueUrl = null;

        try
        {
            queueUrl = await _resolver.GetOrCreateQueueUrlAsync(topic, ct);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            throw new MeshBusException(
                $"Failed to resolve queue URL for topic '{topic}': {ex.Message}",
                ex,
                "SQS");
        }

        var receiveRequest = new ReceiveMessageRequest
        {
            QueueUrl = queueUrl,
            // SQS API supports a maximum of 10 messages per ReceiveMessage call;
            // values of SqsOptions.MaxNumberOfMessages above 10 are clamped here.
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
                    $"Error receiving messages from queue '{topic}': {ex.Message}",
                    ex,
                    "SQS");
            }

            foreach (var sqsMessage in response.Messages)
            {
                try
                {
                    var meshMessage = SqsMessageEnvelope.ToMeshBusMessage<T>(sqsMessage.Body, _serializer);
                    await handler(meshMessage);

                    await _sqsClient.DeleteMessageAsync(queueUrl, sqsMessage.ReceiptHandle, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch
                {
                    // Handler or delete errors are logged silently to keep the loop alive.
                    // Visibility timeout will cause the message to reappear.
                }
            }
        }
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

        _sqsClient.Dispose();
    }
}
