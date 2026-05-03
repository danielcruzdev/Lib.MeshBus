using Google.Cloud.Tasks.V2;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.GoogleCloudTasks.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Google Cloud Tasks provider with MeshBus.
/// </summary>
public static class GoogleCloudTasksMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Google Cloud Tasks as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure Cloud Tasks options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseGoogleCloudTasks(this MeshBusBuilder builder, Action<GoogleCloudTasksOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<CloudTasksClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GoogleCloudTasksOptions>>().Value;
            return BuildClient(options);
        });

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<CloudTasksClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<GoogleCloudTasksOptions>>().Value;
            return new GoogleCloudTasksPublisher(client, serializer, options);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var client = sp.GetRequiredService<CloudTasksClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<GoogleCloudTasksOptions>>().Value;
            return new GoogleCloudTasksSubscriber(client, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Google Cloud Tasks as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseGoogleCloudTasks(this NamedProducerBuilder builder, Action<GoogleCloudTasksOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new GoogleCloudTasksOptions();
            configure(options);

            var client = BuildClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new GoogleCloudTasksPublisher(client, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Google Cloud Tasks as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseGoogleCloudTasks(this NamedConsumerBuilder builder, Action<GoogleCloudTasksOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new GoogleCloudTasksOptions();
            configure(options);

            var client = BuildClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new GoogleCloudTasksSubscriber(client, serializer, options);
        });

        return builder;
    }

    private static CloudTasksClient BuildClient(GoogleCloudTasksOptions options)
    {
        var clientBuilder = new CloudTasksClientBuilder();

        if (!string.IsNullOrEmpty(options.EmulatorHost))
        {
            clientBuilder.Endpoint = options.EmulatorHost;
            clientBuilder.ChannelCredentials = ChannelCredentials.Insecure;
        }

        return clientBuilder.Build();
    }
}
