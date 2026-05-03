using Amazon;
using Amazon.EventBridge;
using Amazon.Runtime;
using Amazon.SQS;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.EventBridge.DependencyInjection;

/// <summary>
/// Extension methods for configuring the AWS EventBridge provider with MeshBus.
/// </summary>
public static class EventBridgeMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use AWS EventBridge as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure EventBridge options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseEventBridge(this MeshBusBuilder builder, Action<EventBridgeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<IAmazonEventBridge>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventBridgeOptions>>().Value;
            return BuildEventBridgeClient(options);
        });

        builder.Services.AddSingleton<IAmazonSQS>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EventBridgeOptions>>().Value;
            return BuildSqsClient(options);
        });

        builder.Services.AddSingleton<EventBridgeOptions>(sp =>
            sp.GetRequiredService<IOptions<EventBridgeOptions>>().Value);

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonEventBridge>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<EventBridgeOptions>();
            return new EventBridgePublisher(client, serializer, options);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var ebClient = sp.GetRequiredService<IAmazonEventBridge>();
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var options = sp.GetRequiredService<EventBridgeOptions>();
            return new EventBridgeSubscriber(ebClient, sqsClient, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use AWS EventBridge as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseEventBridge(this NamedProducerBuilder builder, Action<EventBridgeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new EventBridgeOptions();
            configure(options);

            var client = BuildEventBridgeClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventBridgePublisher(client, serializer, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use AWS EventBridge as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseEventBridge(this NamedConsumerBuilder builder, Action<EventBridgeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new EventBridgeOptions();
            configure(options);

            var ebClient = BuildEventBridgeClient(options);
            var sqsClient = BuildSqsClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new EventBridgeSubscriber(ebClient, sqsClient, serializer, options);
        });

        return builder;
    }

    private static IAmazonEventBridge BuildEventBridgeClient(EventBridgeOptions options)
    {
        var config = new AmazonEventBridgeConfig();

        if (!string.IsNullOrEmpty(options.ServiceUrl))
            config.ServiceURL = options.ServiceUrl;
        else
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.RegionName);

        if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            return new AmazonEventBridgeClient(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                config);

        return new AmazonEventBridgeClient(config);
    }

    private static IAmazonSQS BuildSqsClient(EventBridgeOptions options)
    {
        var config = new AmazonSQSConfig();

        var sqsUrl = options.SqsServiceUrl ?? options.ServiceUrl;
        if (!string.IsNullOrEmpty(sqsUrl))
            config.ServiceURL = sqsUrl;
        else
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.RegionName);

        if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            return new AmazonSQSClient(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                config);

        return new AmazonSQSClient(config);
    }
}
