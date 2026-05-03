using DotPulsar;
using DotPulsar.Abstractions;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Pulsar.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Apache Pulsar provider with MeshBus.
/// </summary>
public static class PulsarMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Apache Pulsar as the messaging provider (single provider mode).
    /// </summary>
    public static MeshBusBuilder UseApachePulsar(this MeshBusBuilder builder, Action<PulsarOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<IPulsarClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PulsarOptions>>().Value;
            return PulsarClient.Builder()
                .ServiceUrl(new Uri(options.ServiceUrl))
                .Build();
        });

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<IPulsarClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new PulsarPublisher(client, serializer);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var client = sp.GetRequiredService<IPulsarClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<PulsarOptions>>().Value;
            return new PulsarSubscriber(client, serializer,
                options.SubscriptionName, options.SubscriptionType, options.InitialPosition);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Apache Pulsar as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseApachePulsar(this NamedProducerBuilder builder, Action<PulsarOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new PulsarOptions();
            configure(options);

            var client = PulsarClient.Builder()
                .ServiceUrl(new Uri(options.ServiceUrl))
                .Build();

            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new PulsarPublisher(client, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Apache Pulsar as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseApachePulsar(this NamedConsumerBuilder builder, Action<PulsarOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new PulsarOptions();
            configure(options);

            var client = PulsarClient.Builder()
                .ServiceUrl(new Uri(options.ServiceUrl))
                .Build();

            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new PulsarSubscriber(client, serializer,
                options.SubscriptionName, options.SubscriptionType, options.InitialPosition);
        });

        return builder;
    }
}
