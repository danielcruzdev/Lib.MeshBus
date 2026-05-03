using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Lib.MeshBus.GoogleCloudTasks.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with Google Cloud Tasks.
///
/// Cloud Tasks is task-queue oriented — the publisher creates HTTP tasks and the
/// subscriber lists and processes pending tasks by polling.
///
/// For local testing you can use a Cloud Tasks emulator.
/// </summary>
public class GoogleCloudTasksScenario
{
    private const string QueueName = "meshbus-demo";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var projectId    = config["GoogleCloudTasks:ProjectId"] ?? "demo-project";
        var locationId   = config["GoogleCloudTasks:LocationId"] ?? "us-central1";
        var targetUrl    = config["GoogleCloudTasks:TargetBaseUrl"] ?? "https://localhost:5001";
        var emulatorHost = config["GoogleCloudTasks:EmulatorHost"];

        Output.Header("Google Cloud Tasks",
            ("Project", projectId),
            ("Location", locationId),
            ("Target URL", targetUrl),
            ("Emulator", emulatorHost ?? "(none)"));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseGoogleCloudTasks(opts =>
        {
            opts.ProjectId      = projectId;
            opts.LocationId     = locationId;
            opts.TargetBaseUrl  = targetUrl;
            opts.EmulatorHost   = emulatorHost;
            opts.AutoCreateQueues = true;
            opts.MaxTasks       = 10;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing (polling tasks from queue)...");

        try
        {
            await subscriber.SubscribeAsync<Order>(QueueName, msg =>
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
        Output.Info($"Publishing {MessageCount} tasks...");
        Console.WriteLine();

        for (int i = 1; i <= MessageCount; i++)
        {
            var order = new Order
            {
                Id = Random.Shared.Next(1000, 9999),
                Product = RandomProduct(),
                Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 200 + 5), 2)
            };
            var message = MeshBusMessage<Order>.Create(order, QueueName);
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for tasks to be processed...");
        await Task.Delay(5_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(QueueName, ct);
    }

    private static string RandomProduct()
    {
        string[] names = ["Keyboard", "Monitor", "Headset", "Webcam", "Mouse", "Hub USB", "SSD", "Cable HDMI", "Desk Lamp", "Mousepad"];
        return names[Random.Shared.Next(names.Length)];
    }
}
