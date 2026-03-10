using Confluent.Kafka;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Kafka.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Kafka provider with MeshBus.
/// </summary>
public static class KafkaMeshBusBuilderExtensions
{
    /// <summary>
    /// Configures MeshBus to use Apache Kafka as the messaging provider (single provider mode).
    /// </summary>
    /// <param name="builder">The MeshBus builder.</param>
    /// <param name="configure">Action to configure Kafka options.</param>
    /// <returns>The MeshBusBuilder for chaining.</returns>
    public static MeshBusBuilder UseKafka(this MeshBusBuilder builder, Action<KafkaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        // Register the Kafka producer
        builder.Services.AddSingleton<IProducer<string, byte[]>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
            var config = new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                Acks = ParseAcks(options.Acks),
                AllowAutoCreateTopics = options.AllowAutoCreateTopics
            };
            return new ProducerBuilder<string, byte[]>(config).Build();
        });

        // Register the Kafka consumer
        builder.Services.AddSingleton<IConsumer<string, byte[]>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<KafkaOptions>>().Value;
            var config = new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.GroupId ?? $"meshbus-{Guid.NewGuid():N}",
                AutoOffsetReset = ParseAutoOffsetReset(options.AutoOffsetReset),
                EnableAutoCommit = options.EnableAutoCommit,
                AllowAutoCreateTopics = options.AllowAutoCreateTopics
            };
            return new ConsumerBuilder<string, byte[]>(config).Build();
        });

        // Register publisher and subscriber
        builder.Services.AddSingleton<IMeshBusPublisher>(sp =>
        {
            var producer = sp.GetRequiredService<IProducer<string, byte[]>>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new KafkaPublisher(producer, serializer);
        });

        builder.Services.AddSingleton<IMeshBusSubscriber>(sp =>
        {
            var consumer = sp.GetRequiredService<IConsumer<string, byte[]>>();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new KafkaSubscriber(consumer, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named producer to use Apache Kafka as the messaging provider.
    /// </summary>
    /// <param name="builder">The named producer builder.</param>
    /// <param name="configure">Action to configure Kafka options.</param>
    /// <returns>The <see cref="NamedProducerBuilder"/> for chaining.</returns>
    public static NamedProducerBuilder UseKafka(this NamedProducerBuilder builder, Action<KafkaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusPublisher>(builder.Name, (sp, _) =>
        {
            var options = new KafkaOptions();
            configure(options);

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                Acks = ParseAcks(options.Acks),
                AllowAutoCreateTopics = options.AllowAutoCreateTopics
            };
            var producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new KafkaPublisher(producer, serializer);
        });

        return builder;
    }

    /// <summary>
    /// Configures a named consumer to use Apache Kafka as the messaging provider.
    /// </summary>
    /// <param name="builder">The named consumer builder.</param>
    /// <param name="configure">Action to configure Kafka options.</param>
    /// <returns>The <see cref="NamedConsumerBuilder"/> for chaining.</returns>
    public static NamedConsumerBuilder UseKafka(this NamedConsumerBuilder builder, Action<KafkaOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddKeyedSingleton<IMeshBusSubscriber>(builder.Name, (sp, _) =>
        {
            var options = new KafkaOptions();
            configure(options);

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = options.BootstrapServers,
                GroupId = options.GroupId ?? $"meshbus-{Guid.NewGuid():N}",
                AutoOffsetReset = ParseAutoOffsetReset(options.AutoOffsetReset),
                EnableAutoCommit = options.EnableAutoCommit,
                AllowAutoCreateTopics = options.AllowAutoCreateTopics
            };
            var consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();
            var serializer = sp.GetRequiredService<IMessageSerializer>();
            return new KafkaSubscriber(consumer, serializer);
        });

        return builder;
    }

    private static Acks ParseAcks(string acks) => acks.ToLowerInvariant() switch
    {
        "all" or "-1" => Acks.All,
        "leader" or "1" => Acks.Leader,
        "none" or "0" => Acks.None,
        _ => Acks.All
    };

    private static AutoOffsetReset ParseAutoOffsetReset(string value) => value.ToLowerInvariant() switch
    {
        "earliest" => AutoOffsetReset.Earliest,
        "latest" => AutoOffsetReset.Latest,
        "error" => AutoOffsetReset.Error,
        _ => AutoOffsetReset.Earliest
    };
}

