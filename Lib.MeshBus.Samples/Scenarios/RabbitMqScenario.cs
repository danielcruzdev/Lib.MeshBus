using System.Collections.Concurrent;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Models;
using Lib.MeshBus.RabbitMQ.DependencyInjection;
using Lib.MeshBus.Samples.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Samples.Scenarios;

/// <summary>
/// Demonstrates publishing and consuming messages with RabbitMQ.
/// Requires RabbitMQ running on localhost:5672 (use docker-compose up rabbitmq).
/// Management UI available at http://localhost:15672 (guest/guest).
/// </summary>
public class RabbitMqScenario
{
    private const string Topic = "orders.demo";
    private const int MessageCount = 5;

    public async Task RunAsync(IConfiguration config, CancellationToken ct)
    {
        var host     = config["RabbitMQ:HostName"] ?? "localhost";
        var port     = int.TryParse(config["RabbitMQ:Port"], out var p) ? p : 5672;
        var userName = config["RabbitMQ:UserName"] ?? "guest";
        var password = config["RabbitMQ:Password"] ?? "guest";

        Output.Header("RabbitMQ",
            ("Broker", $"{host}:{port}"),
            ("Topic", Topic),
            ("UI", $"http://localhost:15672  ({userName}/{password})"));

        // ── 1. Configure DI ─────────────────────────────────────────────────
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseRabbitMq(opts =>
        {
            opts.HostName = host;
            opts.Port     = port;
            opts.UserName = userName;
            opts.Password = password;
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

        await Task.Delay(500, ct);

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
