using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.RabbitMQ.DependencyInjection;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates the multi-broker capability of Lib.MeshBus.
///
/// A single service registers two independent producers (Kafka + RabbitMQ) and
/// two independent consumers (Kafka + RabbitMQ). Messages are published to both
/// brokers simultaneously and consumed independently — zero shared code between them.
///
/// Requires both Kafka and RabbitMQ (use docker-compose up).
/// </summary>
public class MultiProviderScenario
{
    private const string KafkaTopic  = "meshbus.demo.kafka-orders";
    private const string RabbitTopic = "rabbit-orders.demo";
    private const int MessageCount   = 3;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var kafkaBroker  = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        var rabbitHost   = config["RabbitMQ:HostName"] ?? "localhost";
        var rabbitUser   = config["RabbitMQ:UserName"] ?? "guest";
        var rabbitPass   = config["RabbitMQ:Password"] ?? "guest";

        Output.Header("Multi-Broker: Kafka + RabbitMQ",
            ("Kafka",    kafkaBroker),
            ("RabbitMQ", $"{rabbitHost}:5672"),
            ("Pattern",  "2 producers + 2 consumers, fully independent"));

        // ── 1. Configure DI — all brokers in ONE AddMeshBus call ────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus =>
        {
            // Named producers — each targets a different broker
            bus.AddProducer("kafka").UseKafka(opts =>
            {
                opts.BootstrapServers  = kafkaBroker;
                opts.AllowAutoCreateTopics = true;
            });
            bus.AddProducer("rabbit").UseRabbitMq(opts =>
            {
                opts.HostName = rabbitHost;
                opts.UserName = rabbitUser;
                opts.Password = rabbitPass;
            });

            // Named consumers — each targets a different broker
            bus.AddConsumer("kafka").UseKafka(opts =>
            {
                opts.BootstrapServers      = kafkaBroker;
                opts.GroupId               = "meshbus-samples-multi";
                opts.AllowAutoCreateTopics = true;
            });
            bus.AddConsumer("rabbit").UseRabbitMq(opts =>
            {
                opts.HostName = rabbitHost;
                opts.UserName = rabbitUser;
                opts.Password = rabbitPass;
            });
        });

        await using var provider = services.BuildServiceProvider();

        // Resolve by name via factories
        var publisherFactory  = provider.GetRequiredService<IMeshBusPublisherFactory>();
        var subscriberFactory = provider.GetRequiredService<IMeshBusSubscriberFactory>();

        var kafkaPublisher  = publisherFactory.GetPublisher("kafka");
        var rabbitPublisher = publisherFactory.GetPublisher("rabbit");
        var kafkaSub        = subscriberFactory.GetSubscriber("kafka");
        var rabbitSub       = subscriberFactory.GetSubscriber("rabbit");

        // ── 2. Subscribe to both brokers ─────────────────────────────────────
        var kafkaReceived  = new ConcurrentBag<Order>();
        var rabbitReceived = new ConcurrentBag<Order>();

        Output.Info("Subscribing Kafka consumer...");
        await kafkaSub.SubscribeAsync<Order>(KafkaTopic, msg =>
        {
            kafkaReceived.Add(msg.Body);
            Output.Line($"  [← KAFKA]   {msg.Body}", ConsoleColor.Cyan);
            return Task.CompletedTask;
        }, ct);

        Output.Info("Subscribing RabbitMQ consumer...");
        await rabbitSub.SubscribeAsync<Order>(RabbitTopic, msg =>
        {
            rabbitReceived.Add(msg.Body);
            Output.Line($"  [← RABBIT]  {msg.Body}", ConsoleColor.Magenta);
            return Task.CompletedTask;
        }, ct);

        Output.Line("Both consumers subscribed ✓", ConsoleColor.DarkGreen);
        Console.WriteLine();

        await Task.Delay(1_000, ct);

        // ── 3. Publish to both brokers simultaneously ─────────────────────────
        Output.Info($"Publishing {MessageCount} messages to each broker...");
        Console.WriteLine();

        for (int i = 1; i <= MessageCount; i++)
        {
            var kafkaOrder  = new Order { Id = Random.Shared.Next(1000, 9999), Product = $"[K] {RandomProduct()}", Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 200 + 5), 2) };
            var rabbitOrder = new Order { Id = Random.Shared.Next(1000, 9999), Product = $"[R] {RandomProduct()}", Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 200 + 5), 2) };

            await kafkaPublisher.PublishAsync(MeshBusMessage<Order>.Create(kafkaOrder, KafkaTopic), ct);
            Output.Line($"  [→ KAFKA]   {kafkaOrder}", ConsoleColor.Green);

            await rabbitPublisher.PublishAsync(MeshBusMessage<Order>.Create(rabbitOrder, RabbitTopic), ct);
            Output.Line($"  [→ RABBIT]  {rabbitOrder}", ConsoleColor.DarkGreen);
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive on both brokers...");
        await Task.Delay(4_000, ct);

        Output.Summary(kafkaReceived.Count,  MessageCount, "Kafka");
        Output.Summary(rabbitReceived.Count, MessageCount, "RabbitMQ");

        await kafkaSub.UnsubscribeAsync(KafkaTopic, ct);
        await rabbitSub.UnsubscribeAsync(RabbitTopic, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
