using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Lib.MeshBus.EventBridge.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with AWS EventBridge.
///
/// EventBridge is event-driven, so this scenario auto-creates an SQS queue and
/// an EventBridge rule targeting it for pull-based delivery.
///
/// For local testing, start LocalStack with docker compose up -d and ensure
/// "EventBridge:ServiceUrl" is set to "http://localhost:4566" in appsettings.json.
/// </summary>
public class EventBridgeScenario
{
    private const string TopicName = "meshbus-demo";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var serviceUrl = config["EventBridge:ServiceUrl"];
        var region     = config["EventBridge:RegionName"] ?? "us-east-1";
        var accessKey  = config["EventBridge:AccessKey"];
        var secretKey  = config["EventBridge:SecretKey"];
        var eventBus   = config["EventBridge:EventBusName"] ?? "default";
        var source     = config["EventBridge:Source"] ?? "meshbus";
        var accountId  = config["EventBridge:AccountId"] ?? "000000000000";

        var displayTarget = string.IsNullOrWhiteSpace(serviceUrl) ? $"AWS ({region})" : serviceUrl;

        Output.Header("AWS EventBridge",
            ("Target", displayTarget),
            ("Event Bus", eventBus),
            ("Source", source));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseEventBridge(opts =>
        {
            opts.ServiceUrl          = serviceUrl;
            opts.RegionName          = region;
            opts.AccessKey           = accessKey;
            opts.SecretKey           = secretKey;
            opts.EventBusName        = eventBus;
            opts.Source              = source;
            opts.AccountId           = accountId;
            opts.AutoCreateSqsTarget = true;
            opts.SqsServiceUrl       = serviceUrl;
            opts.WaitTimeSeconds     = 1;
            opts.MaxNumberOfMessages = 10;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing (creates SQS queue + EventBridge rule)...");

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

        await Task.Delay(2_000, ct);

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
        await Task.Delay(5_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(TopicName, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
