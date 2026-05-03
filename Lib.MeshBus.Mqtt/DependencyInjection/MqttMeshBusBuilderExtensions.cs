using HiveMQtt.MQTT5.Types;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Mqtt.DependencyInjection;

/// <summary>
/// Extension methods for configuring the MQTT provider with MeshBus.
/// </summary>
public static class MqttMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use MQTT as the messaging provider (single provider mode).
    /// </summary>
    public static MeshBusBuilder UseMqtt(this MeshBusBuilder builder, Action<MqttOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MqttOptions>>().Value;
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new MqttPublisher(MqttClientFactory.CreateAdapter(options), serializer);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MqttOptions>>().Value;
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var qos = ParseQos(options.QualityOfService);
            return new MqttSubscriber(MqttClientFactory.CreateAdapter(options), serializer, qos);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use MQTT as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseMqtt(this NamedProducerBuilder builder, Action<MqttOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new MqttOptions();
            configure(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new MqttPublisher(MqttClientFactory.CreateAdapter(options), serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use MQTT as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseMqtt(this NamedConsumerBuilder builder, Action<MqttOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new MqttOptions();
            configure(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var qos = ParseQos(options.QualityOfService);
            return new MqttSubscriber(MqttClientFactory.CreateAdapter(options), serializer, qos);
        });

        return builder;
    }

    private static QualityOfService ParseQos(string value) => value.ToLowerInvariant() switch
    {
        "atmostonce" or "0" => QualityOfService.AtMostOnceDelivery,
        "exactlyonce" or "2" => QualityOfService.ExactlyOnceDelivery,
        _ => QualityOfService.AtLeastOnceDelivery
    };
}
