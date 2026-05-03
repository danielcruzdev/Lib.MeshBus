using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Mqtt.DependencyInjection;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with MQTT (HiveMQ or any MQTT 5.0 broker).
/// Requires an MQTT broker running on localhost:1883 (use docker-compose up hivemq).
/// </summary>
public class MqttScenario
{
    private const string Topic = "meshbus/demo/orders";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var brokerHost = config["Mqtt:BrokerHost"] ?? "localhost";
        var brokerPort = int.TryParse(config["Mqtt:BrokerPort"], out var p) ? p : 1883;

        Output.Header("HiveMQ / MQTT 5.0",
            ("Broker", $"{brokerHost}:{brokerPort}"),
            ("Topic", Topic),
            ("QoS", "AtLeastOnce"));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseMqtt(opts =>
        {
            opts.BrokerHost = brokerHost;
            opts.BrokerPort = brokerPort;
            opts.QualityOfService = "AtLeastOnce";
            opts.CleanStart = true;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IMeshBusPublisher>();
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

        await Task.Delay(500, ct);

        // ── 3. Publish ───────────────────────────────────────────────────────
        Output.Info($"Publishing {MessageCount} messages...");
        Console.WriteLine();

        for (int i = 1; i <= MessageCount; i++)
        {
            var order = new Order
            {
                Id = Random.Shared.Next(1000, 9999),
                Product = RandomProduct(),
                Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 200 + 5), 2)
            };
            var message = MeshBusMessage<Order>.Create(order, Topic, correlationId: $"session-{i}");
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive...");
        await Task.Delay(3_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(Topic, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
