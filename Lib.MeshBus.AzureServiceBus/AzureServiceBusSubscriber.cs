using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.AzureServiceBus;

/// <summary>
/// Azure Service Bus implementation of IMeshBusSubscriber.
/// </summary>
public class AzureServiceBusSubscriber : IMeshBusSubscriber
{
    private readonly ServiceBusClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly AzureServiceBusOptions _options;
    private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new AzureServiceBusSubscriber.
    /// </summary>
    public AzureServiceBusSubscriber(ServiceBusClient client, IMessageSerializer serializer, AzureServiceBusOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_processors.ContainsKey(topic))
        {
            throw new MeshBusException($"Already subscribed to topic '{topic}'.", new InvalidOperationException(), "AzureServiceBus");
        }

        try
        {
            ServiceBusProcessor processor;

            if (!string.IsNullOrEmpty(_options.SubscriptionName))
            {
                // Topic subscription mode
                processor = _client.CreateProcessor(topic, _options.SubscriptionName, new ServiceBusProcessorOptions
                {
                    MaxConcurrentCalls = _options.MaxConcurrentCalls,
                    AutoCompleteMessages = _options.AutoCompleteMessages
                });
            }
            else
            {
                // Queue mode
                processor = _client.CreateProcessor(topic, new ServiceBusProcessorOptions
                {
                    MaxConcurrentCalls = _options.MaxConcurrentCalls,
                    AutoCompleteMessages = _options.AutoCompleteMessages
                });
            }

            processor.ProcessMessageAsync += async args =>
            {
                var meshMessage = ConvertToMeshBusMessage<T>(args.Message, topic);
                await handler(meshMessage);
            };

            processor.ProcessErrorAsync += args =>
            {
                // Error handling — could be extended with logging
                return Task.CompletedTask;
            };

            _processors[topic] = processor;
            await processor.StartProcessingAsync(cancellationToken);
        }
        catch (ServiceBusException ex)
        {
            throw new MeshBusException(
                $"Failed to subscribe to topic '{topic}': {ex.Message}",
                ex,
                "AzureServiceBus");
        }
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_processors.TryRemove(topic, out var processor))
        {
            try
            {
                await processor.StopProcessingAsync(cancellationToken);
                await processor.DisposeAsync();
            }
            catch (ServiceBusException ex)
            {
                throw new MeshBusException(
                    $"Failed to unsubscribe from topic '{topic}': {ex.Message}",
                    ex,
                    "AzureServiceBus");
            }
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(ServiceBusReceivedMessage message, string topic)
    {
        var body = _serializer.Deserialize<T>(message.Body.ToArray());
        var headers = new Dictionary<string, string>();
        var timestamp = DateTimeOffset.UtcNow;

        foreach (var prop in message.ApplicationProperties)
        {
            if (prop.Key == "meshbus-timestamp" && prop.Value is string tsStr)
            {
                if (DateTimeOffset.TryParse(tsStr, out var ts))
                    timestamp = ts;
            }
            else
            {
                headers[prop.Key] = prop.Value?.ToString() ?? string.Empty;
            }
        }

        return new MeshBusMessage<T>
        {
            Id = message.MessageId ?? Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Headers = headers,
            Body = body!,
            CorrelationId = message.CorrelationId,
            Topic = topic
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            foreach (var processor in _processors.Values)
            {
                try
                {
                    await processor.StopProcessingAsync();
                    await processor.DisposeAsync();
                }
                catch
                {
                    // Best effort cleanup
                }
            }
            _processors.Clear();
            await _client.DisposeAsync();
            _disposed = true;
        }
    }
}

