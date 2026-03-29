using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Lib.MeshBus.RabbitMQ.DependencyInjection;

/// <summary>
/// Extension methods for configuring the RabbitMQ provider with MeshBus.
/// </summary>
public static class RabbitMqMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use RabbitMQ as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure RabbitMQ options.</param>
    /// <returns>The MeshBusBuilder for chaining.</returns>
    public static MeshBusBuilder UseRabbitMq(this MeshBusBuilder builder, Action<RabbitMqOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        // Register the RabbitMQ connection (shared)
        builder.Services.AddSingleton<IConnection>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        // Register publisher
        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var connection = sp.GetRequiredService<IConnection>();
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqPublisher(connection, channel, serializer, options);
        });

        // Register subscriber
        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var connection = sp.GetRequiredService<IConnection>();
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqSubscriber(connection, channel, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use RabbitMQ as the messaging provider.
    /// </summary>
    /// <param name="builder">The named producer builder.</param>
    /// <param name="configure">Action to configure RabbitMQ options.</param>
    /// <returns>The <see cref="NamedProducerBuilder"/> for chaining.</returns>
    public static NamedProducerBuilder UseRabbitMq(this NamedProducerBuilder builder, Action<RabbitMqOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new RabbitMqOptions();
            configure(options);

            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            };
            var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new RabbitMqPublisher(connection, channel, serializer, options, ownsConnection: true);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use RabbitMQ as the messaging provider.
    /// </summary>
    /// <param name="builder">The named consumer builder.</param>
    /// <param name="configure">Action to configure RabbitMQ options.</param>
    /// <returns>The <see cref="NamedConsumerBuilder"/> for chaining.</returns>
    public static NamedConsumerBuilder UseRabbitMq(this NamedConsumerBuilder builder, Action<RabbitMqOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new RabbitMqOptions();
            configure(options);

            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
                VirtualHost = options.VirtualHost
            };
            var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new RabbitMqSubscriber(connection, channel, serializer, options, ownsConnection: true);
        });

        return builder;
    }
}


