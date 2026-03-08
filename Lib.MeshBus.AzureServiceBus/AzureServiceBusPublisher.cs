using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of IMeshBusPublisher.
/// </summary>
public class AzureServiceBusPublisher : IMeshBusPublisher
{
    private readonly ServiceBusClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new AzureServiceBusPublisher.
    /// </summary>
    public AzureServiceBusPublisher(ServiceBusClient client, IMessageSerializer serializer)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var sender = GetOrCreateSender(message.Topic);
            var serviceBusMessage = CreateServiceBusMessage(message);
            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
        }
        catch (ServiceBusException ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Message}",
                ex,
                "AzureServiceBus");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages.ToList();
        if (messageList.Count == 0) return;

        var topic = messageList[0].Topic;

        try
        {
            var sender = GetOrCreateSender(topic);
            var batch = await sender.CreateMessageBatchAsync(cancellationToken);

            var failedMessages = new List<MeshBusMessage<T>>();

            foreach (var message in messageList)
            {
                var serviceBusMessage = CreateServiceBusMessage(message);
                if (!batch.TryAddMessage(serviceBusMessage))
                {
                    // Send current batch and start a new one
                    await sender.SendMessagesAsync(batch, cancellationToken);
                    batch = await sender.CreateMessageBatchAsync(cancellationToken);

                    if (!batch.TryAddMessage(serviceBusMessage))
                    {
                        failedMessages.Add(message);
                    }
                }
            }

            // Send remaining messages
            if (batch.Count > 0)
            {
                await sender.SendMessagesAsync(batch, cancellationToken);
            }

            if (failedMessages.Count > 0)
            {
                throw new MeshBusException(
                    $"Failed to add {failedMessages.Count} message(s) to batch (messages too large).",
                    new InvalidOperationException(),
                    "AzureServiceBus");
            }
        }
        catch (ServiceBusException ex)
        {
            throw new MeshBusException(
                $"Failed to publish batch to topic '{topic}': {ex.Message}",
                ex,
                "AzureServiceBus");
        }
    }

    private ServiceBusSender GetOrCreateSender(string topic)
    {
        return _senders.GetOrAdd(topic, t => _client.CreateSender(t));
    }

    private ServiceBusMessage CreateServiceBusMessage<T>(MeshBusMessage<T> message)
    {
        var body = _serializer.Serialize(message.Body);
        var serviceBusMessage = new ServiceBusMessage(body)
        {
            MessageId = message.Id,
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
            Subject = message.Topic
        };

        foreach (var header in message.Headers)
        {
            serviceBusMessage.ApplicationProperties[header.Key] = header.Value;
        }

        serviceBusMessage.ApplicationProperties["meshbus-timestamp"] = message.Timestamp.ToString("O");

        return serviceBusMessage;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            foreach (var sender in _senders.Values)
            {
                await sender.DisposeAsync();
            }
            _senders.Clear();
            await _client.DisposeAsync();
            _disposed = true;
        }
    }
}

