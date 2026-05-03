using HiveMQtt.Client;
using HiveMQtt.Client.Events;
using HiveMQtt.MQTT5.Types;

namespace Lib.MeshBus.Mqtt;

/// <summary>
/// Abstraction over <see cref="HiveMQClient"/> to enable unit testing via substitution.
/// </summary>
public interface IHiveMqttClient : IAsyncDisposable
{
    /// <summary>
    /// Fires when a message is received on any subscribed topic.
    /// </summary>
    event EventHandler<OnMessageReceivedEventArgs>? OnMessageReceived;

    /// <summary>
    /// Establishes a connection to the MQTT broker.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a topic with the specified Quality of Service.
    /// </summary>
    Task SubscribeAsync(string topic, QualityOfService qos = QualityOfService.AtLeastOnceDelivery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an MQTT 5.0 message.
    /// </summary>
    Task PublishAsync(MQTT5PublishMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a topic.
    /// </summary>
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the MQTT broker.
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
