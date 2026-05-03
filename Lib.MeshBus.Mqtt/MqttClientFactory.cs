using HiveMQtt.Client;
using HiveMQtt.Client.Options;
using Lib.MeshBus.Configuration;

namespace Lib.MeshBus.Mqtt;

/// <summary>
/// Internal factory for creating configured <see cref="IHiveMqttClient"/> instances.
/// </summary>
internal static class MqttClientFactory
{
    internal static IHiveMqttClient CreateAdapter(MqttOptions options)
    {
        var clientId = string.IsNullOrWhiteSpace(options.ClientId)
            ? $"meshbus-{Guid.NewGuid():N}"
            : options.ClientId;

        var builder = new HiveMQClientOptionsBuilder()
            .WithBroker(options.BrokerHost)
            .WithPort(options.BrokerPort)
            .WithClientId(clientId)
            .WithCleanStart(options.CleanStart)
            .WithUseTls(options.UseTls);

        if (!string.IsNullOrWhiteSpace(options.Username))
            builder.WithUserName(options.Username);

        if (!string.IsNullOrWhiteSpace(options.Password))
            builder.WithPassword(options.Password);

        var hiveMqOptions = builder.Build();
        var client = new HiveMQClient(hiveMqOptions);
        return new HiveMqttClientAdapter(client);
    }
}
