using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.AzureServiceBus.DependencyInjection;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with Azure Service Bus.
///
/// Set "AzureServiceBus:ConnectionString" in appsettings.json before running.
/// For local testing, use the Azure Service Bus Emulator:
///   https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator
/// </summary>
public class AzureServiceBusScenario
{
    private const string Queue = "meshbus-demo";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var connectionString = config["AzureServiceBus:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            NotConfigured();
            return;
        }

        // Show only the namespace part of the connection string (avoid leaking keys)
        var displayName = ExtractNamespace(connectionString);

        Output.Header("Azure Service Bus",
            ("Namespace", displayName),
            ("Queue", Queue));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
        {
            opts.ConnectionString   = connectionString;
            opts.AutoCompleteMessages = true;
        }));

        await using var provider = services.BuildServiceProvider();
        var publisher  = provider.GetRequiredService<IMeshBusPublisher>();
        var subscriber = provider.GetRequiredService<IMeshBusSubscriber>();

        // ── 2. Subscribe ─────────────────────────────────────────────────────
        var received = new ConcurrentBag<Order>();

        Output.Info("Subscribing to queue...");
        await subscriber.SubscribeAsync<Order>(Queue, msg =>
        {
            received.Add(msg.Body);
            Output.Received(msg.Body.ToString());
            return Task.CompletedTask;
        }, ct);
        Output.Line("Subscribed ✓", ConsoleColor.DarkGreen);
        Console.WriteLine();

        await Task.Delay(1_000, ct);

        // ── 3. Publish ───────────────────────────────────────────────────────
        Output.Info($"Publishing {MessageCount} messages...");
        Console.WriteLine();

        for (int i = 1; i <= MessageCount; i++)
        {
            var order = new Order { Id = i, Product = $"CloudItem-{i:D2}", Amount = i * 19.99m };
            var message = MeshBusMessage<Order>.Create(order, Queue);
            await publisher.PublishAsync(message, ct);
            Output.Sent(order.ToString());
        }

        // ── 4. Wait and report ───────────────────────────────────────────────
        Console.WriteLine();
        Output.Info("Waiting for messages to arrive...");
        await Task.Delay(4_000, ct);

        Output.Summary(received.Count, MessageCount);

        await subscriber.UnsubscribeAsync(Queue, ct);
    }

    private static void NotConfigured()
    {
        Console.WriteLine();
        Output.Warning("Azure Service Bus is not configured.");
        Console.WriteLine();
        Output.Line("Set the connection string in appsettings.json:", ConsoleColor.Gray);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("    {");
        Console.WriteLine("      \"AzureServiceBus\": {");
        Console.WriteLine("        \"ConnectionString\": \"Endpoint=sb://your-ns.servicebus.windows.net/;...\"");
        Console.WriteLine("      }");
        Console.WriteLine("    }");
        Console.ResetColor();
        Console.WriteLine();
        Output.Line("For local testing, use the Azure Service Bus Emulator:", ConsoleColor.Gray);
        Output.Line("https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator", ConsoleColor.DarkGray);
    }

    private static string ExtractNamespace(string cs)
    {
        // Extract "Endpoint=sb://<namespace>" fragment without exposing the key
        var start = cs.IndexOf("sb://", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return "(configured)";
        var end = cs.IndexOf(';', start);
        return end < 0 ? cs[start..] : cs[start..end];
    }
}
