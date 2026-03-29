using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Sqs.DependencyInjection;

/// <summary>
/// Extension methods for configuring the AWS SQS provider with MeshBus.
/// </summary>
public static class SqsMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use AWS SQS as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure SQS options.</param>
    /// <returns>The <see cref="MeshBusBuilder"/> for chaining.</returns>
    public static MeshBusBuilder UseSqs(this MeshBusBuilder builder, Action<SqsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.AddSingleton<IAmazonSQS>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SqsOptions>>().Value;
            return BuildSqsClient(options);
        });

        builder.Services.AddSingleton<SqsOptions>(sp =>
            sp.GetRequiredService<IOptions<SqsOptions>>().Value);

        builder.Services.AddSingleton<SqsQueueResolver>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonSQS>();
            var options = sp.GetRequiredService<SqsOptions>();
            return new SqsQueueResolver(client, options);
        });

        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonSQS>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = sp.GetRequiredService<SqsQueueResolver>();
            return new SqsPublisher(client, serializer, resolver);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonSQS>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = sp.GetRequiredService<SqsQueueResolver>();
            var options = sp.GetRequiredService<SqsOptions>();
            return new SqsSubscriber(client, serializer, resolver, options);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use AWS SQS as the messaging provider.
    /// </summary>
    public static NamedProducerBuilder UseSqs(this NamedProducerBuilder builder, Action<SqsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new SqsOptions();
            configure(options);

            var client = BuildSqsClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = new SqsQueueResolver(client, options);
            return new SqsPublisher(client, serializer, resolver);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use AWS SQS as the messaging provider.
    /// </summary>
    public static NamedConsumerBuilder UseSqs(this NamedConsumerBuilder builder, Action<SqsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new SqsOptions();
            configure(options);

            var client = BuildSqsClient(options);
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            var resolver = new SqsQueueResolver(client, options);
            return new SqsSubscriber(client, serializer, resolver, options);
        });

        return builder;
    }

    private static IAmazonSQS BuildSqsClient(SqsOptions options)
    {
        var config = new AmazonSQSConfig();

        if (!string.IsNullOrEmpty(options.ServiceUrl))
        {
            config.ServiceURL = options.ServiceUrl;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.RegionName);
        }

        if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
        {
            return new AmazonSQSClient(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                config);
        }

        return new AmazonSQSClient(config);
    }
}
