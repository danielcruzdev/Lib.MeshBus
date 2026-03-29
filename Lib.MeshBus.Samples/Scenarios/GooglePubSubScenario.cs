using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.GooglePubSub.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with Google Cloud Pub/Sub.
///
/// For local testing, start the Pub/Sub emulator with docker compose up -d and
/// ensure "GooglePubSub:EmulatorHost" is set to "localhost:8085" in appsettings.json.
///
/// For Google Cloud, set "GooglePubSub:ProjectId" and ensure your Application Default
/// Credentials are configured (gcloud auth application-default login).
/// </summary>
public class GooglePubSubScenario
{
    private const string Topic = "meshbus-demo";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var projectId    = config["GooglePubSub:ProjectId"] ?? "demo-project";
        var emulatorHost = config["GooglePubSub:EmulatorHost"];

        var displayTarget = string.IsNullOrWhiteSpace(emulatorHost)
            ? $"Google Cloud ({projectId})"
            : $"Emulator ({emulatorHost})";

        Output.Header("Google Cloud Pub/Sub",
            ("Target", displayTarget),
            ("Topic", Topic),
            ("Subscription", $"{Topic}-meshbus-sub"));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseGooglePubSub(opts =>
        {
            opts.ProjectId           = projectId;
            opts.EmulatorHost        = emulatorHost;
            opts.AutoCreateResources = true;
            opts.SubscriptionSuffix  = "meshbus-sub";
            opts.MaxMessages         = 10;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing to topic...");

        try
        {
            await subscriber.SubscribeAsync<Order>(Topic, msg =>
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
            var message = MeshBusMessage<Order>.Create(order, Topic);
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive...");
        await Task.Delay(5_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(Topic, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
