using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.GooglePubSub.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Google Cloud Pub/Sub provider with MeshBus.
/// </summary>
public static class GooglePubSubMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Google Cloud Pub/Sub as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure Pub/Sub options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseGooglePubSub(this MeshBusBuilder builder, Action<GooglePubSubOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<PublisherServiceApiClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GooglePubSubOptions>>().Value;
            return BuildPublisherApiClient(options);
        });

        builder.Services.AddSingleton<SubscriberServiceApiClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GooglePubSubOptions>>().Value;
            return BuildSubscriberApiClient(options);
        });

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var publisherApi = sp.GetRequiredService<PublisherServiceApiClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<GooglePubSubOptions>>().Value;
            return new GooglePubSubPublisher(publisherApi, serializer, options);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var subscriberApi = sp.GetRequiredService<SubscriberServiceApiClient>();
            var publisherApi = sp.GetRequiredService<PublisherServiceApiClient>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<IOptions<GooglePubSubOptions>>().Value;
            return new GooglePubSubSubscriber(subscriberApi, publisherApi, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Google Cloud Pub/Sub as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseGooglePubSub(this NamedProducerBuilder builder, Action<GooglePubSubOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new GooglePubSubOptions();
            configure(options);

            var publisherApi = BuildPublisherApiClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new GooglePubSubPublisher(publisherApi, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Google Cloud Pub/Sub as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseGooglePubSub(this NamedConsumerBuilder builder, Action<GooglePubSubOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new GooglePubSubOptions();
            configure(options);

            var publisherApi = BuildPublisherApiClient(options);
            var subscriberApi = BuildSubscriberApiClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new GooglePubSubSubscriber(subscriberApi, publisherApi, serializer, options);
        });

        return builder;
    }

    private static PublisherServiceApiClient BuildPublisherApiClient(GooglePubSubOptions options)
    {
        var clientBuilder = new PublisherServiceApiClientBuilder();

        if (!string.IsNullOrEmpty(options.EmulatorHost))
        {
            clientBuilder.Endpoint = options.EmulatorHost;
            clientBuilder.ChannelCredentials = ChannelCredentials.Insecure;
        }

        return clientBuilder.Build();
    }

    private static SubscriberServiceApiClient BuildSubscriberApiClient(GooglePubSubOptions options)
    {
        var clientBuilder = new SubscriberServiceApiClientBuilder();

        if (!string.IsNullOrEmpty(options.EmulatorHost))
        {
            clientBuilder.Endpoint = options.EmulatorHost;
            clientBuilder.ChannelCredentials = ChannelCredentials.Insecure;
        }

        return clientBuilder.Build();
    }
}
