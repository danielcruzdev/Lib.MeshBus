using Lib.MeshBus.Samples.Scenarios;
using Microsoft.Extensions.Configuration;

// Load configuration from appsettings.json (override values without touching code)
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

Console.OutputEncoding = System.Text.Encoding.UTF8;

while (true)
{
    Console.Clear();
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine();
    Console.WriteLine("  ╔═══════════════════════════════════════════════╗");
    Console.WriteLine("  ║         Lib.MeshBus — Interactive Demo        ║");
    Console.WriteLine("  ╚═══════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("  Start the brokers first:");
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("    docker compose up -d");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("  ─────────────────────────────────────────────────");
    Console.WriteLine();
    Console.WriteLine("    [1]  Apache Kafka");
    Console.WriteLine("    [2]  RabbitMQ");
    Console.WriteLine("    [3]  Azure Service Bus");
    Console.WriteLine("    [4]  Multi-Broker  (Kafka + RabbitMQ)");
    Console.WriteLine();
    Console.WriteLine("    [0]  Exit");
    Console.WriteLine();
    Console.Write("  Choose a scenario → ");

    var key = Console.ReadKey(intercept: true);
    Console.WriteLine(key.KeyChar);

    if (key.KeyChar == '0') break;

    try
    {
        using var cts = new CancellationTokenSource();

        // Allow Ctrl+C to cancel a running scenario gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        switch (key.KeyChar)
        {
            case '1': await new KafkaScenario().RunAsync(config, cts.Token); break;
            case '2': await new RabbitMqScenario().RunAsync(config, cts.Token); break;
            case '3': await new AzureServiceBusScenario().RunAsync(config, cts.Token); break;
            case '4': await new MultiProviderScenario().RunAsync(config, cts.Token); break;
            default:
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  Invalid option.");
                Console.ResetColor();
                break;
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Scenario cancelled.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Error: {ex.Message}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine();
        Console.WriteLine("  Tip: make sure the broker is running (docker compose up -d)");
        Console.ResetColor();
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write("  Press any key to return to menu...");
    Console.ResetColor();
    Console.ReadKey(intercept: true);
}
