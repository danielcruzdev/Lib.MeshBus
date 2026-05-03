using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Sns.DependencyInjection;

/// <summary>
/// Extension methods for configuring the AWS SNS provider with MeshBus.
/// </summary>
public static class SnsMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use AWS SNS as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure SNS options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseSns(this MeshBusBuilder builder, Action<SnsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SnsOptions>>().Value;
            return BuildSnsClient(options);
        });

        builder.Services.AddSingleton<IAmazonSQS>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SnsOptions>>().Value;
            return BuildSqsClient(options);
        });

        builder.Services.AddSingleton<SnsOptions>(sp =>
            sp.GetRequiredService<IOptions<SnsOptions>>().Value);

        builder.Services.AddSingleton<SnsTopicResolver>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonSimpleNotificationService>();
            var options = sp.GetRequiredService<SnsOptions>();
            return new SnsTopicResolver(client, options);
        });

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonSimpleNotificationService>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = sp.GetRequiredService<SnsTopicResolver>();
            return new SnsPublisher(client, serializer, resolver);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var snsClient = sp.GetRequiredService<IAmazonSimpleNotificationService>();
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = sp.GetRequiredService<SnsTopicResolver>();
            var options = sp.GetRequiredService<SnsOptions>();
            return new SnsSubscriber(snsClient, sqsClient, serializer, resolver, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use AWS SNS as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseSns(this NamedProducerBuilder builder, Action<SnsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new SnsOptions();
            configure(options);

            var client = BuildSnsClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = new SnsTopicResolver(client, options);
            return new SnsPublisher(client, serializer, resolver);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use AWS SNS as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseSns(this NamedConsumerBuilder builder, Action<SnsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new SnsOptions();
            configure(options);

            var snsClient = BuildSnsClient(options);
            var sqsClient = BuildSqsClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = new SnsTopicResolver(snsClient, options);
            return new SnsSubscriber(snsClient, sqsClient, serializer, resolver, options);
        });

        return builder;
    }

    private static IAmazonSimpleNotificationService BuildSnsClient(SnsOptions options)
    {
        var config = new AmazonSimpleNotificationServiceConfig();

        if (!string.IsNullOrEmpty(options.ServiceUrl))
            config.ServiceURL = options.ServiceUrl;
        else
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.RegionName);

        if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            return new AmazonSimpleNotificationServiceClient(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                config);

        return new AmazonSimpleNotificationServiceClient(config);
    }

    private static IAmazonSQS BuildSqsClient(SnsOptions options)
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
