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
  - [Apache Pulsar](#apache-pulsar)
  - [MQTT (HiveMQ)](#mqtt-hivemq)
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
| `meshbus-elasticmq` | ElasticMQ (SQS-compatible) | `9324` |
| `meshbus-pubsub` | Google Pub/Sub emulator | `8085` |
| `meshbus-pulsar` | Apache Pulsar (standalone) | `6650` |
| `meshbus-hivemq` | HiveMQ CE (MQTT 5.0) | `1883` |

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
    [5]  Azure Event Hubs
    [6]  AWS SQS
    [7]  Google Cloud Pub/Sub
    [8]  Azure Event Grid
    [9]  AWS SNS
    [A]  AWS EventBridge
    [B]  Google Cloud Tasks
    [C]  Apache Pulsar
    [D]  HiveMQ / MQTT 5.0

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

### Azure Event Hubs
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.EventHubs
```

### AWS SQS
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.Sqs
```

### Google Cloud Pub/Sub
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.GooglePubSub
```

### Azure Event Grid
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.EventGrid
```

### AWS SNS
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.Sns
```

### AWS EventBridge
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.EventBridge
```

### Google Cloud Tasks
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.GoogleCloudTasks
```

### Apache Pulsar
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.Pulsar
```

### MQTT (HiveMQ / any MQTT 5.0 broker)
```bash
dotnet add package Lib.MeshBus
dotnet add package Lib.MeshBus.Mqtt
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

### Azure Event Hubs

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.EventHubs.DependencyInjection;

services.AddMeshBus(bus => bus.UseEventHubs(opts =>
{
    // Namespace-level connection string (no EntityPath)
    opts.ConnectionString = "Endpoint=sb://my-namespace.servicebus.windows.net/;...";
    opts.ConsumerGroup    = "$Default";  // consumer group for receiving events
}));
```

**Local testing** — no dedicated Event Hubs emulator; use a real Azure namespace.

**Concept mapping:**
| MeshBus | Azure Event Hubs |
|---------|-----------------|
| Topic | Event Hub name |
| Message.Id | `meshbus.id` property |
| Message.Headers | EventData.Properties |
| Message.CorrelationId | `meshbus.correlationId` property |
| PublishAsync | EventHubProducerClient.SendAsync |
| PublishBatchAsync | EventHubProducerClient.CreateBatchAsync + SendAsync |
| SubscribeAsync | EventHubConsumerClient.ReadEventsAsync (all partitions) |

---

### AWS SQS

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Sqs.DependencyInjection;

// Real AWS (credentials from environment / ~/.aws/credentials / instance profile)
services.AddMeshBus(bus => bus.UseSqs(opts =>
{
    opts.RegionName = "us-east-1";
}));

// LocalStack / ElasticMQ
services.AddMeshBus(bus => bus.UseSqs(opts =>
{
    opts.ServiceUrl       = "http://localhost:9324";
    opts.AccountId        = "000000000000";
    opts.AccessKey        = "test";
    opts.SecretKey        = "test";
    opts.AutoCreateQueues = true;   // auto-create queues on first use
    opts.WaitTimeSeconds  = 20;     // long polling (0–20 seconds)
}));
```

**Local testing** — start ElasticMQ with `docker compose up -d elasticmq`.

**Concept mapping:**
| MeshBus | AWS SQS |
|---------|---------|
| Topic | SQS Queue name |
| Message (all fields) | JSON envelope in SQS message body |
| PublishAsync | AmazonSQSClient.SendMessageAsync |
| PublishBatchAsync | AmazonSQSClient.SendMessageBatchAsync (up to 10/request) |
| SubscribeAsync | Long-polling loop (ReceiveMessageAsync + DeleteMessageAsync) |

---

### Google Cloud Pub/Sub

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.GooglePubSub.DependencyInjection;

// Google Cloud (uses Application Default Credentials)
services.AddMeshBus(bus => bus.UseGooglePubSub(opts =>
{
    opts.ProjectId           = "my-gcp-project";
    opts.SubscriptionSuffix  = "meshbus-sub"; // subscription = "{topic}-meshbus-sub"
    opts.AutoCreateResources = true;           // auto-create topics and subscriptions
}));

// Local emulator
services.AddMeshBus(bus => bus.UseGooglePubSub(opts =>
{
    opts.ProjectId           = "demo-project";
    opts.EmulatorHost        = "localhost:8085";
    opts.AutoCreateResources = true;
}));
```

**Local testing** — start the emulator with `docker compose up -d pubsub-emulator`.

**Concept mapping:**
| MeshBus | Google Pub/Sub |
|---------|---------------|
| Topic | Pub/Sub Topic (`projects/{project}/topics/{name}`) |
| Message.Id | `meshbus.id` attribute |
| Message.Headers | PubsubMessage.Attributes |
| Message.CorrelationId | `meshbus.correlationId` attribute |
| PublishAsync | PublisherServiceApiClient.PublishAsync |
| SubscribeAsync | Polling: SubscriberServiceApiClient.PullAsync + AcknowledgeAsync |
| Subscription name | `projects/{project}/subscriptions/{topic}-{SubscriptionSuffix}` |

---

### Azure Event Grid

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.EventGrid.DependencyInjection;

services.AddMeshBus(bus => bus.UseEventGrid(opts =>
{
    // Publishing — Event Grid Topic
    opts.TopicEndpoint = "https://my-topic.eastus-1.eventgrid.azure.net/api/events";
    opts.AccessKey     = "your-topic-access-key";

    // Subscribing — Event Grid Namespace (pull delivery)
    opts.NamespaceEndpoint  = "https://my-namespace.eastus-1.eventgrid.azure.net";
    opts.NamespaceAccessKey = "your-namespace-access-key";
    opts.NamespaceTopicName = "meshbus-demo";
    opts.SubscriptionName   = "meshbus-sub";
    opts.MaxEvents          = 10;
    opts.MaxWaitTimeSeconds = 10;
}));
```

**Concept mapping:**
| MeshBus | Azure Event Grid |
|---------|-----------------|
| Topic | EventGridEvent.Subject |
| Message.Id | EventGridEvent.Id |
| Message.Headers | Data dictionary |
| PublishAsync | EventGridPublisherClient.SendEventAsync |
| PublishBatchAsync | EventGridPublisherClient.SendEventsAsync |
| SubscribeAsync | EventGridReceiverClient.ReceiveAsync (pull delivery) |

---

### AWS SNS

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Sns.DependencyInjection;

// Real AWS
services.AddMeshBus(bus => bus.UseSns(opts =>
{
    opts.RegionName       = "us-east-1";
    opts.AutoCreateTopics = true;
    opts.AutoCreateSqsSubscription = true; // auto-creates SQS queue for consuming
}));

// LocalStack
services.AddMeshBus(bus => bus.UseSns(opts =>
{
    opts.ServiceUrl              = "http://localhost:4566";
    opts.AccessKey               = "test";
    opts.SecretKey               = "test";
    opts.AccountId               = "000000000000";
    opts.AutoCreateTopics        = true;
    opts.AutoCreateSqsSubscription = true;
    opts.SqsServiceUrl           = "http://localhost:4566";
    opts.WaitTimeSeconds         = 20;
}));
```

**Concept mapping:**
| MeshBus | AWS SNS |
|---------|---------|
| Topic | SNS Topic name (auto-resolved to ARN) |
| Message (all fields) | JSON envelope in SNS message body |
| PublishAsync | AmazonSNSClient.PublishAsync |
| PublishBatchAsync | AmazonSNSClient.PublishBatchAsync (up to 10/req) |
| SubscribeAsync | SQS queue subscribed to SNS topic (long-polling) |

---

### AWS EventBridge

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.EventBridge.DependencyInjection;

// Real AWS
services.AddMeshBus(bus => bus.UseEventBridge(opts =>
{
    opts.RegionName        = "us-east-1";
    opts.EventBusName      = "default";
    opts.Source             = "meshbus";
    opts.AutoCreateSqsTarget = true; // auto-creates SQS queue + rule for consuming
}));

// LocalStack
services.AddMeshBus(bus => bus.UseEventBridge(opts =>
{
    opts.ServiceUrl          = "http://localhost:4566";
    opts.AccessKey           = "test";
    opts.SecretKey           = "test";
    opts.AccountId           = "000000000000";
    opts.EventBusName        = "default";
    opts.Source              = "meshbus";
    opts.AutoCreateSqsTarget = true;
    opts.SqsServiceUrl       = "http://localhost:4566";
    opts.WaitTimeSeconds     = 20;
}));
```

**Concept mapping:**
| MeshBus | AWS EventBridge |
|---------|----------------|
| Topic | DetailType (event type on the bus) |
| Message (all fields) | JSON envelope in Detail |
| PublishAsync | AmazonEventBridgeClient.PutEventsAsync |
| PublishBatchAsync | PutEventsAsync (up to 10 entries/request) |
| SubscribeAsync | SQS queue + EventBridge rule targeting it |

---

### Google Cloud Tasks

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.GoogleCloudTasks.DependencyInjection;

// Google Cloud
services.AddMeshBus(bus => bus.UseGoogleCloudTasks(opts =>
{
    opts.ProjectId       = "my-gcp-project";
    opts.LocationId      = "us-central1";
    opts.TargetBaseUrl   = "https://my-service.run.app";
    opts.AutoCreateQueues = true;
}));

// Emulator
services.AddMeshBus(bus => bus.UseGoogleCloudTasks(opts =>
{
    opts.ProjectId       = "demo-project";
    opts.LocationId      = "us-central1";
    opts.TargetBaseUrl   = "https://localhost:5001";
    opts.EmulatorHost    = "localhost:8123";
    opts.AutoCreateQueues = true;
}));
```

**Concept mapping:**
| MeshBus | Google Cloud Tasks |
|---------|--------------------|
| Topic | Queue name |
| Message (all fields) | HTTP task body (JSON envelope) |
| PublishAsync | CloudTasksClient.CreateTaskAsync |
| PublishBatchAsync | Sequential CreateTaskAsync calls |
| SubscribeAsync | Polling: ListTasks + DeleteTask |

---

### Apache Pulsar

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Pulsar.DependencyInjection;

services.AddMeshBus(bus => bus.UseApachePulsar(opts =>
{
    opts.ServiceUrl        = "pulsar://localhost:6650"; // Pulsar broker URL
    opts.SubscriptionName  = "meshbus-subscription";   // Consumer subscription name
    opts.SubscriptionType  = "Shared";                 // "Exclusive", "Shared", "Failover", "KeyShared"
    opts.InitialPosition   = "Earliest";               // "Earliest" or "Latest"
}));
```

**Concept mapping:**
| MeshBus | Apache Pulsar |
|---------|---------------|
| Topic | Persistent topic (e.g. `persistent://public/default/orders`) |
| Message.Id | MessageMetadata property `meshbus-message-id` |
| Message.Headers | MessageMetadata custom properties |
| Message.CorrelationId | MessageMetadata property `meshbus-correlation-id` |
| PublishAsync | IProducer\<T\>.Send (one producer per topic, cached) |
| SubscribeAsync | IConsumer\<T\>.Receive polling loop per topic |

**Local testing** — start with `docker compose up -d pulsar`.

---

### MQTT (HiveMQ)

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Mqtt.DependencyInjection;

// Minimal (works with any MQTT 5.0 broker — HiveMQ, Mosquitto, EMQX, …)
services.AddMeshBus(bus => bus.UseMqtt(opts =>
{
    opts.BrokerHost        = "localhost";      // Broker hostname
    opts.BrokerPort        = 1883;             // 1883 plain, 8883 TLS
    opts.ClientId          = "my-service";     // Optional (auto-generated if empty)
    opts.UseTls            = false;            // Enable TLS/SSL
    opts.Username          = "";               // Optional credentials
    opts.Password          = "";
    opts.QualityOfService  = "AtLeastOnce";   // "AtMostOnce", "AtLeastOnce", "ExactlyOnce"
    opts.CleanStart        = true;             // MQTT 5 CleanStart flag
}));
```

**Concept mapping:**
| MeshBus | MQTT |
|---------|------|
| Topic | MQTT topic string (e.g. `meshbus/orders/created`) |
| Message.Id | UserProperty `meshbus-message-id` |
| Message.Headers | MQTT 5 UserProperties |
| Message.CorrelationId | MQTT 5 CorrelationData (UTF-8 encoded) |
| PublishAsync | HiveMQClient.PublishAsync (MQTT5PublishMessage) |
| SubscribeAsync | HiveMQClient.SubscribeAsync + OnMessageReceived event |

**Local testing** — start with `docker compose up -d hivemq`.

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

| Feature | Kafka | RabbitMQ | Azure Service Bus | Azure Event Hubs | AWS SQS | Google Pub/Sub | Azure Event Grid | AWS SNS | AWS EventBridge | Google Cloud Tasks | Apache Pulsar | MQTT (HiveMQ) |
|---------|:-----:|:--------:|:-----------------:|:----------------:|:-------:|:--------------:|:----------------:|:-------:|:---------------:|:------------------:|:-------------:|:-------------:|
| PublishAsync | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| PublishBatchAsync | ✅ | ✅ | ✅ (native batch) | ✅ (native batch) | ✅ (up to 10/req) | ✅ | ✅ (native batch) | ✅ (up to 10/req) | ✅ (up to 10/req) | ✅ | ✅ | ✅ |
| SubscribeAsync | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (pull delivery) | ✅ (via SQS) | ✅ (via SQS) | ✅ (polling) | ✅ (polling loop) | ✅ (event-driven) |
| UnsubscribeAsync | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Headers/Metadata | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| CorrelationId | ✅ | ✅ (native) | ✅ (native) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (native) |
| Durable Messages | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (QoS ≥1) |
| Custom Serialization | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| IAsyncDisposable | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Local Emulator | ✅ (KRaft) | ✅ | ✅ (official) | — | ✅ (ElasticMQ) | ✅ (gcloud) | — | ✅ (LocalStack) | ✅ (LocalStack) | ✅ (emulator) | ✅ (standalone) | ✅ (HiveMQ CE) |

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
| ElasticMQ (AWS SQS) | `meshbus-elasticmq` | `9324` (SQS API) · `9325` (UI) |
| Google Pub/Sub Emulator | `meshbus-pubsub` | `8085` |
| Apache Pulsar (standalone) | `meshbus-pulsar` | `6650` (binary) · `8081` (Admin API) |
| HiveMQ CE (MQTT 5.0) | `meshbus-hivemq` | `1883` (MQTT) |

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
dotnet test --filter "FullyQualifiedName~EventHubs"
dotnet test --filter "FullyQualifiedName~SQS"
dotnet test --filter "FullyQualifiedName~GooglePubSub"
dotnet test --filter "FullyQualifiedName~EventGrid"
dotnet test --filter "FullyQualifiedName~SNS"
dotnet test --filter "FullyQualifiedName~EventBridge"
dotnet test --filter "FullyQualifiedName~GoogleCloudTasks"
dotnet test --filter "FullyQualifiedName~Pulsar"
dotnet test --filter "FullyQualifiedName~Mqtt"

# With code coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Roadmap

### ✅ Phase 1 — MVP
- [x] Apache Kafka
- [x] RabbitMQ
- [x] Azure Service Bus

### ✅ Phase 2 — Cloud Providers
- [x] Azure Event Hubs
- [x] AWS SQS
- [x] Google Pub/Sub
- [x] Azure Event Grid
- [x] AWS SNS
- [x] AWS EventBridge
- [x] Google Cloud Tasks

### ⬜ Phase 3 — Enterprise & Specialized
- [ ] ActiveMQ
- [ ] IBM MQ
- [x] HiveMQ (MQTT)
- [x] Apache Pulsar

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
├── Lib.MeshBus.EventHubs/          # Azure Event Hubs provider
│   └── DependencyInjection/
│       └── EventHubsMeshBusBuilderExtensions.cs
├── Lib.MeshBus.Sqs/                # AWS SQS provider
│   └── DependencyInjection/
│       └── SqsMeshBusBuilderExtensions.cs
├── Lib.MeshBus.GooglePubSub/       # Google Cloud Pub/Sub provider
│   └── DependencyInjection/
│       └── GooglePubSubMeshBusBuilderExtensions.cs
├── Lib.MeshBus.EventGrid/          # Azure Event Grid provider
│   └── DependencyInjection/
│       └── EventGridMeshBusBuilderExtensions.cs
├── Lib.MeshBus.Sns/                # AWS SNS provider
│   └── DependencyInjection/
│       └── SnsMeshBusBuilderExtensions.cs
├── Lib.MeshBus.EventBridge/        # AWS EventBridge provider
│   └── DependencyInjection/
│       └── EventBridgeMeshBusBuilderExtensions.cs
├── Lib.MeshBus.GoogleCloudTasks/   # Google Cloud Tasks provider
│   └── DependencyInjection/
│       └── GoogleCloudTasksMeshBusBuilderExtensions.cs
├── Lib.MeshBus.Pulsar/             # Apache Pulsar provider
│   └── DependencyInjection/
│       └── PulsarMeshBusBuilderExtensions.cs
├── Lib.MeshBus.Mqtt/               # MQTT 5.0 provider (HiveMQ / Mosquitto / EMQX)
│   └── DependencyInjection/
│       └── MqttMeshBusBuilderExtensions.cs
├── Lib.MeshBus.Tests/              # Unit tests
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

