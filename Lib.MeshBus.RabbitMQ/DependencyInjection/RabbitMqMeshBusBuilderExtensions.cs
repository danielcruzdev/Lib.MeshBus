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
    /// Configures MeshBus to use RabbitMQ as the messaging provider.
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
}

