using HiveMQtt.Client;
using HiveMQtt.Client.Events;
using HiveMQtt.Client.Options;
using HiveMQtt.MQTT5.Types;

namespace Lib.MeshBus.Mqtt;

/// <summary>
/// Adapts <see cref="HiveMQClient"/> to the <see cref="IHiveMqttClient"/> interface.
/// </summary>
internal sealed class HiveMqttClientAdapter : IHiveMqttClient
{
    private readonly HiveMQClient _client;

    internal HiveMqttClientAdapter(HiveMQClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _client.OnMessageReceived += (s, e) => OnMessageReceived?.Invoke(s, e);
    }

    /// <inheritdoc />
    public event EventHandler<OnMessageReceivedEventArgs>? OnMessageReceived;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
        => await _client.ConnectAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public async Task SubscribeAsync(string topic, QualityOfService qos = QualityOfService.AtLeastOnceDelivery, CancellationToken cancellationToken = default)
    {
        await _client.SubscribeAsync(topic, qos).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(MQTT5PublishMessage message, CancellationToken cancellationToken = default)
        => await _client.PublishAsync(message, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        await _client.UnsubscribeAsync(topic).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        => await _client.DisconnectAsync(new DisconnectOptions()).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try { await DisconnectAsync().ConfigureAwait(false); } catch { /* ignore disconnect errors on dispose */ }
        _client.Dispose();
    }
}
