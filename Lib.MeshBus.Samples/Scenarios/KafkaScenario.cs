using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with Apache Kafka.
/// Requires Kafka running on localhost:9092 (use docker-compose up kafka).
/// </summary>
public class KafkaScenario
{
    private const string Topic = "meshbus.demo.orders";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        var groupId = config["Kafka:GroupId"] ?? "meshbus-samples";

        Output.Header("Apache Kafka",
            ("Broker", bootstrapServers),
            ("Topic", Topic),
            ("Group", groupId));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseKafka(opts =>
        {
            opts.BootstrapServers = bootstrapServers;
            opts.GroupId = groupId;
            opts.AllowAutoCreateTopics = true;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing to topic...");
        await subscriber.SubscribeAsync<Order>(Topic, msg =>
        {
            received.Add(msg.Body);
            Output.Received(msg.Body.ToString());
            return Task.CompletedTask;
        }, ct);
        Output.Line("Subscribed ✓", ConsoleColor.DarkGreen);
        Console.WriteLine();

        // Give the consumer loop time to start
        await Task.Delay(1_000, ct);

        // ── 3. Publish ───────────────────────────────────────────────────────
        Output.Info($"Publishing {MessageCount} messages...");
        Console.WriteLine();

        for (int i = 1; i <= MessageCount; i++)
        {
            var order = new Order { Id = Random.Shared.Next(1000, 9999), Product = RandomProduct(), Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 200 + 5), 2) };
            var message = MeshBusMessage<Order>.Create(order, Topic);
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive...");
        await Task.Delay(4_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(Topic, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
