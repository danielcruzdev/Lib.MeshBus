# 🚌 Lib.MeshBus

**A unified .NET library for multi-broker messaging.**

Connect your application to any message broker through a single interface. Switching brokers = changing **one line of code**.

[![CI](https://github.com/danielcruzdev/Lib.MeshBus/actions/workflows/ci.yml/badge.svg)](https://github.com/danielcruzdev/Lib.MeshBus/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

---

## 📋 Table of Contents

- [Overview](#overview)
- [Running the Samples](#running-the-samples)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Multiple Providers in the Same Service](#multiple-providers-in-the-same-service)
- [Provider Configuration](#provider-configuration)
  - [Apache Kafka](#apache-kafka)
  - [RabbitMQ](#rabbitmq)
  - [Azure Service Bus](#azure-service-bus)
- [Migration Guide](#migration-guide)
- [API Reference](#api-reference)
- [Compatibility Matrix](#compatibility-matrix)
- [Testing with Docker](#testing-with-docker)
- [Roadmap](#roadmap)
- [Contributing](#contributing)

---

## 🚀 Running the Samples

The fastest way to see Lib.MeshBus in action is through the interactive demo console — **no configuration required**, just Docker.

### 1. Start the brokers

```bash
docker compose up -d
```

This starts:
| Container | Service | Port |
|-----------|---------|------|
| `meshbus-kafka` | Apache Kafka (KRaft) | `9092` |
| `meshbus-rabbitmq` | RabbitMQ | `5672` · Management UI: [localhost:15672](http://localhost:15672) (guest/guest) |

### 2. Run the demo console

```bash
dotnet run --project Lib.MeshBus.Samples
```

You'll get an interactive menu:

```
  ╔═══════════════════════════════════════════════╗
  ║         Lib.MeshBus — Interactive Demo        ║
  ╚═══════════════════════════════════════════════╝

    [1]  Apache Kafka
    [2]  RabbitMQ
    [3]  Azure Service Bus
    [4]  Multi-Broker  (Kafka + RabbitMQ)

    [0]  Exit
```

Each scenario subscribes to a topic, publishes 5 messages, and prints the received messages — all using the same `IMeshBusPublisher` / `IMeshBusSubscriber` interfaces, regardless of the broker.

### 3. Configure (optional)

Broker addresses are read from `Lib.MeshBus.Samples/appsettings.json`. The defaults match the Docker Compose setup, so no changes are needed for local testing:

```json
{
  "Kafka":           { "BootstrapServers": "localhost:9092", "GroupId": "meshbus-samples" },
  "RabbitMQ":        { "HostName": "localhost", "UserName": "guest", "Password": "guest" },
  "AzureServiceBus": { "ConnectionString": "" }
}
```

To test the **Azure Service Bus** scenario, fill in `ConnectionString` with a real namespace or an [emulator connection string](https://learn.microsoft.com/en-us/azure/service-bus-messaging/overview-emulator).

### 4. Stop the brokers

```bash
docker compose down
```

---

## Overview

**Lib.MeshBus** solves the coupling problem between .NET applications and broker-specific SDKs. With a unified abstraction, you:

- ✅ **Publish and consume messages** with the same interface, regardless of the broker
- ✅ **Multiple providers simultaneously** — use Kafka, RabbitMQ and Azure Service Bus in the same service
- ✅ **Switch brokers** by changing only the configuration (zero changes to business logic)
- ✅ **Reduce the learning curve** — learn one API, use with any broker
- ✅ **Pluggable serialization** — System.Text.Json by default, extensible to Protobuf/MessagePack
- ✅ **Async-first** — all operations are asynchronous
- ✅ **Native DI** — full integration with `IServiceCollection`

### Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                        Your .NET Application                     │
│                                                                  │
│  IMeshBusPublisherFactory      IMeshBusSubscriberFactory         │
│  └─ GetPublisher("name")       └─ GetSubscriber("name")          │
│                                                                  │
│  IMeshBusPublisher             IMeshBusSubscriber                │
│  (single-provider via direct DI) (single-provider via direct DI) │
└─────────────────────────────┬────────────────────────────────────┘
                              │
┌─────────────────────────────▼────────────────────────────────────┐
│                     Lib.MeshBus (Core)                            │
│  IMeshBusPublisher          IMeshBusSubscriber                    │
│  IMeshBusPublisherFactory   IMeshBusSubscriberFactory             │
│  MeshBusMessage<T>          IMessageSerializer                    │
│  MeshBusBuilder             ServiceCollectionExtensions           │
└─────────────────────────────┬────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────┐     ┌─────────────┐     ┌──────────────────┐
│  .Kafka     │     │  .RabbitMQ  │     │ .AzureServiceBus │
└─────────────┘     └─────────────┘     └──────────────────┘
```

---

## Multiple Providers in the Same Service

A single service can register **as many producers and consumers as needed**, each pointing to a different broker and topic. Each instance is identified by a **unique name** and resolved via `IMeshBusPublisherFactory` / `IMeshBusSubscriberFactory`.

### Example 1 — Kafka Producer + RabbitMQ Producer, Kafka Consumer

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus =>
{
    // Producers
    bus.AddProducer("kafka-orders").UseKafka(opts =>
    {
        opts.BootstrapServers = "localhost:9092";
    });
    bus.AddProducer("rabbit-events").UseRabbitMq(opts =>
    {
        opts.HostName = "localhost";
    });

    // Consumers
    bus.AddConsumer("kafka-payments").UseKafka(opts =>
    {
        opts.BootstrapServers = "localhost:9092";
        opts.GroupId = "payments-group";
    });
});
```

### Example 2 — Full mix (Azure Service Bus + RabbitMQ + Kafka)

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.AzureServiceBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus =>
{
    // Producers
    bus.AddProducer("asb-notifications").UseAzureServiceBus(opts =>
    {
        opts.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
    });
    bus.AddProducer("rabbit-reports").UseRabbitMq(opts =>
    {
        opts.HostName = "localhost";
    });
    bus.AddProducer("kafka-audit").UseKafka(opts =>
    {
        opts.BootstrapServers = "localhost:9092";
    });

    // Consumers
    bus.AddConsumer("rabbit-orders").UseRabbitMq(opts =>
    {
        opts.HostName = "localhost";
    });
    bus.AddConsumer("asb-payments").UseAzureServiceBus(opts =>
    {
        opts.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
        opts.SubscriptionName = "payments-sub";
    });
});
```

### Using the Factory

```csharp
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Models;

public class MyService
{
    private readonly IMeshBusPublisherFactory _publisherFactory;
    private readonly IMeshBusSubscriberFactory _subscriberFactory;

    public MyService(
        IMeshBusPublisherFactory publisherFactory,
        IMeshBusSubscriberFactory subscriberFactory)
    {
        _publisherFactory = publisherFactory;
        _subscriberFactory = subscriberFactory;
    }

    public async Task SendOrderAsync(Order order)
    {
        var publisher = _publisherFactory.GetPublisher("kafka-orders");
        var message = MeshBusMessage<Order>.Create(order, "orders-created");
        await publisher.PublishAsync(message);
    }

    public async Task StartListeningAsync(CancellationToken ct)
    {
        var subscriber = _subscriberFactory.GetSubscriber("rabbit-orders");
        await subscriber.SubscribeAsync<Order>("orders-created", async msg =>
        {
            Console.WriteLine($"Order received via RabbitMQ: {msg.Body.Id}");
        }, ct);
    }
}
```

> **Note:** The single-provider mode (`UseKafka()` directly on `MeshBusBuilder`) continues to work normally. The named mode (`AddProducer(name).UseKafka()`) is complementary — use it when you need multiple brokers in the same service.

---

## Installation

Install the core package and the desired provider:

### Apache Kafka
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.Kafka
```

### RabbitMQ
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.RabbitMQ
```

### Azure Service Bus
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.AzureServiceBus
```

---

## Quick Start

### 1. Configure the Provider (Program.cs / Startup.cs)

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configure MeshBus with Kafka
builder.Services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "localhost:9092";
    opts.GroupId = "my-application";
}));
```

### 2. Publish Messages

```csharp
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Models;

public class OrderService
{
    private readonly IMeshBusPublisher _publisher;

    public OrderService(IMeshBusPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task CreateOrderAsync(Order order)
    {
        var message = MeshBusMessage<Order>.Create(order, "orders-created");
        message.CorrelationId = order.Id.ToString();
        message.Headers["source"] = "orders-api";

        await _publisher.PublishAsync(message);
    }

    public async Task CreateOrdersBatchAsync(IEnumerable<Order> orders)
    {
        var messages = orders.Select(o =>
            MeshBusMessage<Order>.Create(o, "orders-created"));

        await _publisher.PublishBatchAsync(messages);
    }
}
```

### 3. Consume Messages

```csharp
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Models;

public class OrderConsumer : BackgroundService
{
    private readonly IMeshBusSubscriber _subscriber;

    public OrderConsumer(IMeshBusSubscriber subscriber)
    {
        _subscriber = subscriber;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _subscriber.SubscribeAsync<Order>("orders-created", async message =>
        {
            Console.WriteLine($"Order received: {message.Body.Id}");
            Console.WriteLine($"CorrelationId: {message.CorrelationId}");
            Console.WriteLine($"Timestamp: {message.Timestamp}");

            // Process the order...
        }, stoppingToken);
    }
}
```

---

## Provider Configuration

### Apache Kafka

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;

services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "localhost:9092";       // Kafka broker address(es)
    opts.GroupId = "my-application";               // Consumer group
    opts.AutoOffsetReset = "earliest";             // "earliest", "latest", "error"
    opts.Acks = "all";                             // "all", "leader", "none"
    opts.EnableAutoCommit = true;                  // Auto-commit offsets
    opts.AllowAutoCreateTopics = true;             // Auto-create topics
}));
```

**Concept mapping:**
| MeshBus | Kafka |
|---------|-------|
| Topic | Topic |
| Message.Id | Message Key |
| Message.Headers | Kafka Headers |
| PublishAsync | ProduceAsync |
| SubscribeAsync | Consume (background loop) |

---

### RabbitMQ

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus => bus.UseRabbitMq(opts =>
{
    opts.HostName = "localhost";                   // RabbitMQ hostname
    opts.Port = 5672;                              // Port
    opts.UserName = "guest";                       // Username
    opts.Password = "guest";                       // Password
    opts.VirtualHost = "/";                        // Virtual host
    opts.ExchangeName = "meshbus";                 // Exchange name
    opts.ExchangeType = "topic";                   // "direct", "fanout", "topic", "headers"
    opts.Durable = true;                           // Durable exchanges/queues
    opts.AutoDelete = false;                       // Auto-delete when no consumers
}));
```

**Concept mapping:**
| MeshBus | RabbitMQ |
|---------|----------|
| Topic | Routing Key → Queue (`meshbus.{topic}`) |
| Message.Id | BasicProperties.MessageId |
| Message.Headers | BasicProperties.Headers |
| Message.CorrelationId | BasicProperties.CorrelationId |
| PublishAsync | BasicPublishAsync (via exchange) |
| SubscribeAsync | BasicConsumeAsync (with AsyncEventingBasicConsumer) |

---

### Azure Service Bus

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.AzureServiceBus.DependencyInjection;

// Queue mode
services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
    opts.MaxConcurrentCalls = 5;                   // Parallel processing
    opts.AutoCompleteMessages = true;              // Auto-complete messages
}));

// Topic + Subscription mode
services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
    opts.SubscriptionName = "my-subscription";     // Subscription name
    opts.MaxConcurrentCalls = 10;
}));
```

**Concept mapping:**
| MeshBus | Azure Service Bus |
|---------|-------------------|
| Topic | Queue or Topic name |
| Message.Id | ServiceBusMessage.MessageId |
| Message.Headers | ApplicationProperties |
| Message.CorrelationId | ServiceBusMessage.CorrelationId |
| PublishAsync | SendMessageAsync |
| PublishBatchAsync | CreateMessageBatchAsync + SendMessagesAsync |
| SubscribeAsync | ServiceBusProcessor.StartProcessingAsync |

---

## 🔄 Migration Guide

### Migrating from Kafka to RabbitMQ

**Before (Kafka):**
```csharp
using Lib.MeshBus.Kafka.DependencyInjection;

services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "localhost:9092";
    opts.GroupId = "my-app";
}));
```

**After (RabbitMQ):**
```csharp
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus => bus.UseRabbitMq(opts =>
{
    opts.HostName = "localhost";
    opts.UserName = "guest";
    opts.Password = "guest";
}));
```

**What changes:** Only the `using` and the `.UseKafka()` → `.UseRabbitMq()` call with the new provider's options.

**What does NOT change:** Absolutely nothing in the publish and consume code. `IMeshBusPublisher`, `IMeshBusSubscriber`, `MeshBusMessage<T>` — all remain identical.

---

### Migrating from RabbitMQ to Azure Service Bus

**Before (RabbitMQ):**
```csharp
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus => bus.UseRabbitMq(opts =>
{
    opts.HostName = "localhost";
}));
```

**After (Azure Service Bus):**
```csharp
using Lib.MeshBus.AzureServiceBus.DependencyInjection;

services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
    opts.SubscriptionName = "my-subscription"; // required for topics
}));
```

---

### Migrating from Azure Service Bus to Kafka

**Before (Azure Service Bus):**
```csharp
using Lib.MeshBus.AzureServiceBus.DependencyInjection;

services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://...";
}));
```

**After (Kafka):**
```csharp
using Lib.MeshBus.Kafka.DependencyInjection;

services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "kafka-cluster:9092";
    opts.GroupId = "my-app";
}));
```

---

### Migration Checklist

1. ✅ Change the NuGet package (`dotnet remove package Lib.MeshBus.Kafka` → `dotnet add package Lib.MeshBus.RabbitMQ`)
2. ✅ Update the `using` in the configuration file
3. ✅ Change `.UseKafka(...)` to `.UseRabbitMq(...)` with the new provider's options
4. ✅ Ensure topics/queues exist in the new broker (or use auto-create)
5. ✅ **No changes** required in controllers, services or consumers

---

## API Reference

### IMeshBusPublisher

```csharp
public interface IMeshBusPublisher : IAsyncDisposable
{
    Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken ct = default);
    Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken ct = default);
}
```

### IMeshBusSubscriber

```csharp
public interface IMeshBusSubscriber : IAsyncDisposable
{
    Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken ct = default);
    Task UnsubscribeAsync(string topic, CancellationToken ct = default);
}
```

### IMeshBusPublisherFactory

Used when multiple producers are registered with distinct names.

```csharp
public interface IMeshBusPublisherFactory
{
    IMeshBusPublisher GetPublisher(string name);
}
```

### IMeshBusSubscriberFactory

Used when multiple consumers are registered with distinct names.

```csharp
public interface IMeshBusSubscriberFactory
{
    IMeshBusSubscriber GetSubscriber(string name);
}
```

### MeshBusBuilder — Multi-Provider Extensions

```csharp
// Returns a NamedProducerBuilder — chain .UseKafka() / .UseRabbitMq() / .UseAzureServiceBus()
public NamedProducerBuilder AddProducer(string name);

// Returns a NamedConsumerBuilder — chain .UseKafka() / .UseRabbitMq() / .UseAzureServiceBus()
public NamedConsumerBuilder AddConsumer(string name);
```

### MeshBusMessage\<T\>

```csharp
public class MeshBusMessage<T>
{
    string Id { get; set; }                          // Auto-generated GUID
    DateTimeOffset Timestamp { get; set; }           // Auto-generated UTC
    Dictionary<string, string> Headers { get; set; } // Custom headers
    T Body { get; set; }                             // Payload
    string? CorrelationId { get; set; }              // Tracing
    string Topic { get; set; }                       // Topic/queue

    static MeshBusMessage<T> Create(T body, string topic, string? correlationId = null);
}
```

### IMessageSerializer

```csharp
public interface IMessageSerializer
{
    byte[] Serialize<T>(T obj);
    T? Deserialize<T>(byte[] data);
}
```

To use a custom serializer, register it before calling `AddMeshBus()`:

```csharp
services.AddSingleton<IMessageSerializer, MyProtobufSerializer>();
services.AddMeshBus(bus => bus.UseKafka(...));
```

---

## Compatibility Matrix

| Feature | Kafka | RabbitMQ | Azure Service Bus |
|---------|:-----:|:--------:|:-----------------:|
| PublishAsync | ✅ | ✅ | ✅ |
| PublishBatchAsync | ✅ | ✅ | ✅ (native batch) |
| SubscribeAsync | ✅ | ✅ | ✅ |
| UnsubscribeAsync | ✅ | ✅ | ✅ |
| Headers/Metadata | ✅ | ✅ | ✅ |
| CorrelationId | ✅ | ✅ (native) | ✅ (native) |
| Durable Messages | ✅ | ✅ | ✅ |
| Custom Serialization | ✅ | ✅ | ✅ |
| IAsyncDisposable | ✅ | ✅ | ✅ |

---

## Testing with Docker

A `docker-compose.yml` is included at the root of the repository. It starts **Apache Kafka** and **RabbitMQ** with a single command — no manual configuration needed.

```bash
# Start all brokers
docker compose up -d

# View logs
docker compose logs -f

# Stop and remove containers + volumes
docker compose down -v
```

| Service | Container | Ports |
|---------|-----------|-------|
| Apache Kafka (KRaft) | `meshbus-kafka` | `9092` |
| RabbitMQ | `meshbus-rabbitmq` | `5672` (AMQP) · `15672` (Management UI) |

**RabbitMQ Management UI:** http://localhost:15672 — credentials: `guest / guest`

### Azure Service Bus (Local Emulator)

For Azure Service Bus, Microsoft provides an official emulator (requires EULA acceptance):

```bash
# Ref: https://github.com/Azure/azure-service-bus-emulator-installer
docker run -d \
  --name servicebus-emulator \
  -p 5672:5672 \
  -e ACCEPT_EULA=Y \
  mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
```

Then set the connection string in `Lib.MeshBus.Samples/appsettings.json`:

```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
  }
}
```

---

### Run the Tests

```bash
# All unit tests (no infrastructure required)
dotnet test

# Tests for a specific provider only
dotnet test --filter "FullyQualifiedName~Kafka"
dotnet test --filter "FullyQualifiedName~RabbitMQ"
dotnet test --filter "FullyQualifiedName~AzureServiceBus"

# With code coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Roadmap

### ✅ Phase 1 — MVP (Current)
- [x] Apache Kafka
- [x] RabbitMQ
- [x] Azure Service Bus

### ⬜ Phase 2 — Cloud Providers
- [ ] Azure Event Hubs
- [ ] Azure Event Grid
- [ ] AWS SQS
- [ ] AWS SNS
- [ ] AWS EventBridge
- [ ] Google Pub/Sub
- [ ] Google Cloud Tasks

### ⬜ Phase 3 — Enterprise & Specialized
- [ ] ActiveMQ
- [ ] IBM MQ
- [ ] HiveMQ (MQTT)
- [ ] Apache Pulsar

---

## Project Structure

```
Lib.MeshBus/
├── Lib.MeshBus/                    # Core — interfaces and abstractions
│   ├── Abstractions/
│   │   ├── IMeshBusPublisher.cs
│   │   ├── IMeshBusPublisherFactory.cs   ← factory for multi-producer
│   │   ├── IMeshBusSubscriber.cs
│   │   ├── IMeshBusSubscriberFactory.cs  ← factory for multi-consumer
│   │   └── IMessageSerializer.cs
│   ├── Configuration/
│   │   ├── MeshBusOptions.cs
│   │   ├── KafkaOptions.cs
│   │   ├── RabbitMqOptions.cs
│   │   └── AzureServiceBusOptions.cs
│   ├── DependencyInjection/
│   │   ├── MeshBusBuilder.cs             ← AddProducer() / AddConsumer()
│   │   ├── NamedProducerBuilder.cs       ← named producer builder
│   │   ├── NamedConsumerBuilder.cs       ← named consumer builder
│   │   ├── MeshBusPublisherFactory.cs    ← factory implementation
│   │   ├── MeshBusSubscriberFactory.cs   ← factory implementation
│   │   └── ServiceCollectionExtensions.cs
│   ├── Exceptions/
│   │   └── MeshBusException.cs
│   ├── Models/
│   │   └── MeshBusMessage.cs
│   └── Serialization/
│       └── SystemTextJsonSerializer.cs
├── Lib.MeshBus.Kafka/              # Kafka provider
│   └── DependencyInjection/
│       └── KafkaMeshBusBuilderExtensions.cs  ← UseKafka() for Named builders
├── Lib.MeshBus.RabbitMQ/           # RabbitMQ provider
│   └── DependencyInjection/
│       └── RabbitMqMeshBusBuilderExtensions.cs
├── Lib.MeshBus.AzureServiceBus/    # Azure Service Bus provider
│   └── DependencyInjection/
│       └── AzureServiceBusMeshBusBuilderExtensions.cs
├── Lib.MeshBus.Tests/              # Unit tests (148 tests)
├── .github/PRD.md                  # Product Requirements Document
└── README.md                       # Documentation
```

---

## Contributing

1. Fork the repository
2. Create a branch: `git checkout -b feature/my-feature`
3. Make your changes
4. Run the tests: `dotnet test`
5. Open a Pull Request

---

## License

MIT License — see [LICENSE](LICENSE) for details.

