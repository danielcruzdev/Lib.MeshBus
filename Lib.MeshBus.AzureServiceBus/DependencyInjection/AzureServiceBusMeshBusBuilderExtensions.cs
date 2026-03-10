using Azure.Messaging.ServiceBus;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.AzureServiceBus.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Azure Service Bus provider with MeshBus.
/// </summary>
public static class AzureServiceBusMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Azure Service Bus as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure Azure Service Bus options.</param>
    /// <returns>The MeshBusBuilder for chaining.</returns>
    public static MeshBusBuilder UseAzureServiceBus(this MeshBusBuilder builder, Action<AzureServiceBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        // Register the ServiceBusClient
        builder.Services.AddSingleton<ServiceBusClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;

            if (!string.IsNullOrEmpty(options.ConnectionString))
            {
                return new ServiceBusClient(options.ConnectionString);
            }

            throw new InvalidOperationException(
                "ConnectionString must be provided for Azure Service Bus. " +
                "For managed identity support, register ServiceBusClient manually.");
        });

        // Register publisher
        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new AzureServiceBusPublisher(client, serializer);
        });

        // Register subscriber
        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var client = sp.GetRequiredService<ServiceBusClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
            return new AzureServiceBusSubscriber(client, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Azure Service Bus as the messaging provider.
    /// </summary>
    /// <param name="builder">The named producer builder.</param>
    /// <param name="configure">Action to configure Azure Service Bus options.</param>
    /// <returns>The <see cref="NamedProducerBuilder"/> for chaining.</returns>
    public static NamedProducerBuilder UseAzureServiceBus(this NamedProducerBuilder builder, Action<AzureServiceBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new AzureServiceBusOptions();
            configure(options);

            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionString must be provided for Azure Service Bus. " +
                    "For managed identity support, register ServiceBusClient manually.");
            }

            var client = new ServiceBusClient(options.ConnectionString);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new AzureServiceBusPublisher(client, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Azure Service Bus as the messaging provider.
    /// </summary>
    /// <param name="builder">The named consumer builder.</param>
    /// <param name="configure">Action to configure Azure Service Bus options.</param>
    /// <returns>The <see cref="NamedConsumerBuilder"/> for chaining.</returns>
    public static NamedConsumerBuilder UseAzureServiceBus(this NamedConsumerBuilder builder, Action<AzureServiceBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new AzureServiceBusOptions();
            configure(options);

            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionString must be provided for Azure Service Bus. " +
                    "For managed identity support, register ServiceBusClient manually.");
            }

            var client = new ServiceBusClient(options.ConnectionString);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new AzureServiceBusSubscriber(client, serializer, options);
        });

        return builder;
    }
}


