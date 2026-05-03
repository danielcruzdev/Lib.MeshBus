using System.Collections.Concurrent;
using System.Text;
using HiveMQtt.Client.Events;
using HiveMQtt.MQTT5.Types;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Mqtt;

/// <summary>
/// MQTT implementation of <see cref="IMeshBusSubscriber"/> using HiveMQtt.
/// </summary>
public class MqttSubscriber : IMeshBusSubscriber
{
    private readonly IHiveMqttClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly QualityOfService _qos;
    private readonly ConcurrentDictionary<string, Func<MQTT5PublishMessage, Task>> _topicHandlers = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private bool _connected;
    private bool _eventHandlerRegistered;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="MqttSubscriber"/> using a pre-built client (for testing).
    /// </summary>
    public MqttSubscriber(IHiveMqttClient client, IMessageSerializer serializer,
        QualityOfService qos = QualityOfService.AtLeastOnceDelivery)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _qos = qos;
    }

    /// <summary>
    /// Creates a new <see cref="MqttSubscriber"/> using <see cref="MqttOptions"/>.
    /// </summary>
    public MqttSubscriber(IOptions<MqttOptions> options, IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _client = MqttClientFactory.CreateAdapter(options.Value);
        _qos = ParseQos(options.Value.QualityOfService);
    }

    /// <inheritdoc />
    public async Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_topicHandlers.ContainsKey(topic))
            throw new MeshBusException($"Already subscribed to topic '{topic}'.", new InvalidOperationException(), "MQTT");

        EnsureEventHandlerRegistered();

        _topicHandlers[topic] = async (mqttMessage) =>
        {
            var meshBusMessage = ConvertToMeshBusMessage<T>(mqttMessage, topic);
            await handler(meshBusMessage);
        };

        await EnsureConnectedAsync(cancellationToken);
        await _client.SubscribeAsync(topic, _qos, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_topicHandlers.TryRemove(topic, out _))
            await _client.UnsubscribeAsync(topic, cancellationToken);
    }

    private void EnsureEventHandlerRegistered()
    {
        if (_eventHandlerRegistered) return;

        _client.OnMessageReceived += OnMessageReceived;
        _eventHandlerRegistered = true;
    }

    private void OnMessageReceived(object? sender, OnMessageReceivedEventArgs args)
    {
        var topic = args.PublishMessage.Topic;
        if (topic is null) return;

        if (_topicHandlers.TryGetValue(topic, out var handler))
            _ = DispatchAsync(handler, args.PublishMessage);
    }

    private static async Task DispatchAsync(Func<MQTT5PublishMessage, Task> handler, MQTT5PublishMessage message)
    {
        try
        {
            await handler(message);
        }
        catch (Exception)
        {
            // Swallow handler exceptions to prevent crash of the subscriber loop
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connected) return;

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (!_connected)
            {
                await _client.ConnectAsync(cancellationToken);
                _connected = true;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private MeshBusMessage<T> ConvertToMeshBusMessage<T>(MQTT5PublishMessage mqttMessage, string topic)
    {
        var payload = mqttMessage.Payload ?? [];
        var body = _serializer.Deserialize<T>(payload);
        var headers = new Dictionary<string, string>();
        string? correlationId = null;
        var timestamp = DateTimeOffset.UtcNow;
        var messageId = Guid.NewGuid().ToString();

        if (mqttMessage.CorrelationData is { Length: > 0 } corrData)
            correlationId = Encoding.UTF8.GetString(corrData);

        if (mqttMessage.UserProperties is not null)
        {
            foreach (var prop in mqttMessage.UserProperties)
            {
                switch (prop.Key)
                {
                    case "meshbus-message-id":
                        messageId = prop.Value;
                        break;
                    case "meshbus-timestamp":
                        if (DateTimeOffset.TryParse(prop.Value, out var ts))
                            timestamp = ts;
                        break;
                    default:
                        headers[prop.Key] = prop.Value;
                        break;
                }
            }
        }

        return new MeshBusMessage<T>
        {
            Id = messageId,
            Timestamp = timestamp,
            Headers = headers,
            Body = body!,
            CorrelationId = correlationId,
            Topic = topic
        };
    }

    private static QualityOfService ParseQos(string value) => value.ToLowerInvariant() switch
    {
        "atmostonce" or "0" => QualityOfService.AtMostOnceDelivery,
        "exactlyonce" or "2" => QualityOfService.ExactlyOnceDelivery,
        _ => QualityOfService.AtLeastOnceDelivery
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _connectLock.Dispose();

        foreach (var topic in _topicHandlers.Keys.ToList())
        {
            try { await _client.UnsubscribeAsync(topic); } catch { }
        }

        _topicHandlers.Clear();
        await _client.DisposeAsync();
    }
}
