using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.EventHubs.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Azure Event Hubs provider with MeshBus.
/// </summary>
public static class EventHubsMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Azure Event Hubs as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure Event Hubs options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseEventHubs(this MeshBusBuilder builder, Action<EventHubsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventHubsOptions>>().Value;
            var serializer = sp.GetRequiredService<IMessageSerializer>();

            if (string.IsNullOrEmpty(options.ConnectionString))
                throw new InvalidOperationException(
                    "ConnectionString must be provided for Azure Event Hubs.");

            return new EventHubsPublisher(options.ConnectionString, serializer);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventHubsOptions>>().Value;
            var serializer = sp.GetRequiredService<IMessageSerializer>();

            if (string.IsNullOrEmpty(options.ConnectionString))
                throw new InvalidOperationException(
                    "ConnectionString must be provided for Azure Event Hubs.");

            return new EventHubsSubscriber(options.ConnectionString, options.ConsumerGroup, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Azure Event Hubs as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseEventHubs(this NamedProducerBuilder builder, Action<EventHubsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new EventHubsOptions();
            configure(options);

            if (string.IsNullOrEmpty(options.ConnectionString))
                throw new InvalidOperationException(
                    "ConnectionString must be provided for Azure Event Hubs.");

            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventHubsPublisher(options.ConnectionString, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Azure Event Hubs as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseEventHubs(this NamedConsumerBuilder builder, Action<EventHubsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new EventHubsOptions();
            configure(options);

            if (string.IsNullOrEmpty(options.ConnectionString))
                throw new InvalidOperationException(
                    "ConnectionString must be provided for Azure Event Hubs.");

            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventHubsSubscriber(options.ConnectionString, options.ConsumerGroup, serializer);
        });

        return builder;
    }
}
