using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Lib.MeshBus.EventGrid.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with Azure Event Grid.
///
/// Requires an Event Grid topic (for publishing) and an Event Grid namespace
/// topic + subscription (for pull delivery / subscribing).
/// </summary>
public class EventGridScenario
{
    private const string TopicName = "meshbus-demo";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var topicEndpoint        = config["EventGrid:TopicEndpoint"];
        var accessKey            = config["EventGrid:AccessKey"];
        var namespaceEndpoint    = config["EventGrid:NamespaceEndpoint"];
        var namespaceAccessKey   = config["EventGrid:NamespaceAccessKey"];
        var namespaceTopicName   = config["EventGrid:NamespaceTopicName"] ?? TopicName;
        var subscriptionName     = config["EventGrid:SubscriptionName"] ?? "meshbus-sub";

        Output.Header("Azure Event Grid",
            ("Topic Endpoint", topicEndpoint ?? "(not set)"),
            ("Namespace Topic", namespaceTopicName));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseEventGrid(opts =>
        {
            opts.TopicEndpoint        = topicEndpoint;
            opts.AccessKey            = accessKey;
            opts.NamespaceEndpoint    = namespaceEndpoint;
            opts.NamespaceAccessKey   = namespaceAccessKey;
            opts.NamespaceTopicName   = namespaceTopicName;
            opts.SubscriptionName     = subscriptionName;
            opts.MaxEvents            = 10;
            opts.MaxWaitTimeSeconds   = 5;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing via pull delivery...");

        try
        {
            await subscriber.SubscribeAsync<Order>(TopicName, msg =>
            {
                received.Add(msg.Body);
                Output.Received(msg.Body.ToString());
                return Task.CompletedTask;
            }, ct);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  Failed to subscribe: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("  Press any key to return...");
            Console.ReadKey(intercept: true);
            return;
        }

        Output.Line("Subscribed ✓", ConsoleColor.DarkGreen);
        Console.WriteLine();

        await Task.Delay(1_000, ct);

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
            var message = MeshBusMessage<Order>.Create(order, TopicName);
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive...");
        await Task.Delay(6_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(TopicName, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
