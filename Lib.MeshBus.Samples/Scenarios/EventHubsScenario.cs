using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.EventHubs.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with Azure Event Hubs.
///
/// Set "EventHubs:ConnectionString" in appsettings.json before running.
/// The connection string must be a namespace-level shared access key (no EntityPath).
/// Set "EventHubs:EventHubName" to specify the event hub to use.
/// </summary>
public class EventHubsScenario
{
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var connectionString = config["EventHubs:ConnectionString"];
        var eventHubName = config["EventHubs:EventHubName"] ?? "meshbus-demo";
        var consumerGroup = config["EventHubs:ConsumerGroup"] ?? "$Default";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            NotConfigured();
            return;
        }

        Output.Header("Azure Event Hubs",
            ("Event Hub", eventHubName),
            ("Consumer Group", consumerGroup));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseEventHubs(opts =>
        {
            opts.ConnectionString = connectionString;
            opts.ConsumerGroup    = consumerGroup;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing to event hub...");
        await subscriber.SubscribeAsync<Order>(eventHubName, msg =>
        {
            received.Add(msg.Body);
            Output.Received(msg.Body.ToString());
            return Task.CompletedTask;
        }, ct);
        Output.Line("Subscribed ✓", ConsoleColor.DarkGreen);
        Console.WriteLine();

        // Give the consumer loop time to start reading from all partitions
        await Task.Delay(3_000, ct);

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
            var message = MeshBusMessage<Order>.Create(order, eventHubName);
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive...");
        await Task.Delay(5_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(eventHubName, ct);
    }

    private static void NotConfigured()
    {
        Console.WriteLine();
        Output.Warning("Azure Event Hubs is not configured.");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Set the following keys in appsettings.json:");
        Console.WriteLine("    \"EventHubs\": {");
        Console.WriteLine("      \"ConnectionString\": \"Endpoint=sb://my-namespace.servicebus.windows.net/;...\"");
        Console.WriteLine("      \"EventHubName\": \"meshbus-demo\"");
        Console.WriteLine("    }");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("  Press any key to return...");
        Console.ReadKey(intercept: true);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
