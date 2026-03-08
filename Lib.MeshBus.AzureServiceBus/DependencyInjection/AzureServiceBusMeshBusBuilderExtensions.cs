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
    /// Configures MeshBus to use Azure Service Bus as the messaging provider.
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
}

