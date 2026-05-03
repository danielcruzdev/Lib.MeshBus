using Azure;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.Namespaces;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.EventGrid.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Azure Event Grid provider with MeshBus.
/// </summary>
public static class EventGridMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Azure Event Grid as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure Event Grid options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseEventGrid(this MeshBusBuilder builder, Action<EventGridOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<EventGridPublisherClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventGridOptions>>().Value;
            return BuildPublisherClient(options);
        });

        builder.Services.AddSingleton<EventGridReceiverClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventGridOptions>>().Value;
            return BuildReceiverClient(options);
        });

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<EventGridPublisherClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventGridPublisher(client, serializer);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var receiverClient = sp.GetRequiredService<EventGridReceiverClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<EventGridOptions>>().Value;
            return new EventGridSubscriber(receiverClient, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Azure Event Grid as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseEventGrid(this NamedProducerBuilder builder, Action<EventGridOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new EventGridOptions();
            configure(options);

            var client = BuildPublisherClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventGridPublisher(client, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Azure Event Grid as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseEventGrid(this NamedConsumerBuilder builder, Action<EventGridOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new EventGridOptions();
            configure(options);

            var receiverClient = BuildReceiverClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventGridSubscriber(receiverClient, serializer, options);
        });

        return builder;
    }

    private static EventGridPublisherClient BuildPublisherClient(EventGridOptions options)
    {
        if (string.IsNullOrEmpty(options.TopicEndpoint))
            throw new InvalidOperationException(
                "TopicEndpoint must be provided for Azure Event Grid.");

        if (!string.IsNullOrEmpty(options.AccessKey))
            return new EventGridPublisherClient(
                new Uri(options.TopicEndpoint),
                new AzureKeyCredential(options.AccessKey));

        throw new InvalidOperationException(
            "AccessKey must be provided for Azure Event Grid. " +
            "For managed identity support, register EventGridPublisherClient manually.");
    }

    private static EventGridReceiverClient BuildReceiverClient(EventGridOptions options)
    {
        if (string.IsNullOrEmpty(options.NamespaceEndpoint))
            throw new InvalidOperationException(
                "NamespaceEndpoint must be provided for Event Grid pull delivery.");

        if (string.IsNullOrEmpty(options.NamespaceTopicName))
            throw new InvalidOperationException(
                "NamespaceTopicName must be provided for Event Grid pull delivery.");

        if (string.IsNullOrEmpty(options.SubscriptionName))
            throw new InvalidOperationException(
                "SubscriptionName must be provided for Event Grid pull delivery.");

        if (!string.IsNullOrEmpty(options.NamespaceAccessKey))
            return new EventGridReceiverClient(
                new Uri(options.NamespaceEndpoint),
                options.NamespaceTopicName,
                options.SubscriptionName,
                new AzureKeyCredential(options.NamespaceAccessKey));

        throw new InvalidOperationException(
            "NamespaceAccessKey must be provided for Event Grid pull delivery. " +
            "For managed identity support, register EventGridReceiverClient manually.");
    }
}
