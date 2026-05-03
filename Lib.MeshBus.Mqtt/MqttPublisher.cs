using System.Text;
using HiveMQtt.MQTT5.Types;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Mqtt;

/// <summary>
/// MQTT implementation of <see cref="IMeshBusPublisher"/> using HiveMQtt.
/// </summary>
public class MqttPublisher : IMeshBusPublisher
{
    private readonly IHiveMqttClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private bool _connected;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="MqttPublisher"/> using a pre-built client (for testing).
    /// </summary>
    public MqttPublisher(IHiveMqttClient client, IMessageSerializer serializer)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <summary>
    /// Creates a new <see cref="MqttPublisher"/> using <see cref="MqttOptions"/>.
    /// </summary>
    public MqttPublisher(IOptions<MqttOptions> options, IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _client = MqttClientFactory.CreateAdapter(options.Value);
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var payload = _serializer.Serialize(message.Body);
            var mqttMessage = BuildPublishMessage(message, payload);
            await _client.PublishAsync(mqttMessage, cancellationToken);
        }
        catch (MeshBusException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Message}",
                ex,
                "MQTT");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var exceptions = new List<Exception>();

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(message, cancellationToken);
            }
            catch (MeshBusException ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new MeshBusException(
                $"Failed to publish {exceptions.Count} message(s) in batch.",
                new AggregateException(exceptions),
                "MQTT");
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

    private static MQTT5PublishMessage BuildPublishMessage<T>(MeshBusMessage<T> message, byte[] payload)
    {
        var mqttMessage = new MQTT5PublishMessage
        {
            Topic = message.Topic,
            Payload = payload,
            UserProperties = new Dictionary<string, string>
            {
                ["meshbus-message-id"] = message.Id,
                ["meshbus-timestamp"] = message.Timestamp.ToString("O")
            }
        };

        if (!string.IsNullOrEmpty(message.CorrelationId))
            mqttMessage.CorrelationData = Encoding.UTF8.GetBytes(message.CorrelationId);

        foreach (var header in message.Headers)
            mqttMessage.UserProperties[header.Key] = header.Value;

        return mqttMessage;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _connectLock.Dispose();
        await _client.DisposeAsync();
    }
}
