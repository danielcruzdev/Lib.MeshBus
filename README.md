# 🚌 Lib.MeshBus

**Uma biblioteca .NET unificada para mensageria multi-broker.**

Conecte sua aplicação a qualquer broker de mensageria com uma interface única. Trocar de broker = alterar **uma linha de código**.

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

---

## 📋 Índice

- [Visão Geral](#visão-geral)
- [Instalação](#instalação)
- [Quick Start](#quick-start)
- [Configuração por Provider](#configuração-por-provider)
  - [Apache Kafka](#apache-kafka)
  - [RabbitMQ](#rabbitmq)
  - [Azure Service Bus](#azure-service-bus)
- [Guia de Migração](#guia-de-migração)
- [API Reference](#api-reference)
- [Tabela de Compatibilidade](#tabela-de-compatibilidade)
- [Roadmap](#roadmap)
- [Contribuindo](#contribuindo)

---

## Visão Geral

**Lib.MeshBus** resolve o problema de acoplamento entre aplicações .NET e SDKs específicos de brokers de mensageria. Com uma abstração unificada, você:

- ✅ **Publica e consome mensagens** com a mesma interface, independente do broker
- ✅ **Troca de broker** alterando apenas a configuração (zero mudanças no código de negócio)
- ✅ **Reduz a curva de aprendizado** — aprenda uma API, use com qualquer broker
- ✅ **Serialização plugável** — System.Text.Json por padrão, extensível para Protobuf/MessagePack
- ✅ **Async-first** — todas as operações são assíncronas
- ✅ **DI nativo** — integração total com `IServiceCollection`

### Arquitetura

```
┌─────────────────────────────────────────────┐
│            Sua Aplicação .NET               │
│                                             │
│  IMeshBusPublisher    IMeshBusSubscriber     │
│  (injetados via DI)  (injetados via DI)     │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│           Lib.MeshBus (Core)                │
│  Interfaces │ Modelos │ Serialização │ DI   │
└──────────────────┬──────────────────────────┘
                   │
     ┌─────────────┼─────────────┐
     ▼             ▼             ▼
┌─────────┐ ┌──────────┐ ┌────────────────┐
│  Kafka  │ │ RabbitMQ │ │ Azure Service  │
│         │ │          │ │ Bus            │
└─────────┘ └──────────┘ └────────────────┘
```

---

## Instalação

Instale o pacote core e o provider desejado:

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

### 1. Configurar o Provider (Program.cs / Startup.cs)

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configurar MeshBus com Kafka
builder.Services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "localhost:9092";
    opts.GroupId = "minha-aplicacao";
}));
```

### 2. Publicar Mensagens

```csharp
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Models;

public class PedidoService
{
    private readonly IMeshBusPublisher _publisher;

    public PedidoService(IMeshBusPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task CriarPedidoAsync(Pedido pedido)
    {
        // Criar e publicar mensagem
        var message = MeshBusMessage<Pedido>.Create(pedido, "pedidos-criados");
        message.CorrelationId = pedido.Id.ToString();
        message.Headers["origem"] = "api-pedidos";

        await _publisher.PublishAsync(message);
    }

    public async Task CriarPedidosEmLoteAsync(IEnumerable<Pedido> pedidos)
    {
        var messages = pedidos.Select(p =>
            MeshBusMessage<Pedido>.Create(p, "pedidos-criados"));

        await _publisher.PublishBatchAsync(messages);
    }
}
```

### 3. Consumir Mensagens

```csharp
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Models;

public class PedidoConsumer : BackgroundService
{
    private readonly IMeshBusSubscriber _subscriber;

    public PedidoConsumer(IMeshBusSubscriber subscriber)
    {
        _subscriber = subscriber;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _subscriber.SubscribeAsync<Pedido>("pedidos-criados", async message =>
        {
            Console.WriteLine($"Pedido recebido: {message.Body.Id}");
            Console.WriteLine($"CorrelationId: {message.CorrelationId}");
            Console.WriteLine($"Timestamp: {message.Timestamp}");

            // Processar o pedido...
        }, stoppingToken);
    }
}
```

---

## Configuração por Provider

### Apache Kafka

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka.DependencyInjection;

services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "localhost:9092";       // Endereço(s) dos brokers Kafka
    opts.GroupId = "minha-aplicacao";                // Consumer group
    opts.AutoOffsetReset = "earliest";              // "earliest", "latest", "error"
    opts.Acks = "all";                              // "all", "leader", "none"
    opts.EnableAutoCommit = true;                   // Auto-commit de offsets
    opts.AllowAutoCreateTopics = true;              // Criação automática de tópicos
}));
```

**Mapeamento de conceitos:**
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
    opts.HostName = "localhost";                    // Hostname do RabbitMQ
    opts.Port = 5672;                               // Porta
    opts.UserName = "guest";                        // Usuário
    opts.Password = "guest";                        // Senha
    opts.VirtualHost = "/";                         // Virtual host
    opts.ExchangeName = "meshbus";                  // Nome do exchange
    opts.ExchangeType = "topic";                    // "direct", "fanout", "topic", "headers"
    opts.Durable = true;                            // Exchanges/queues duráveis
    opts.AutoDelete = false;                        // Auto-delete quando sem consumers
}));
```

**Mapeamento de conceitos:**
| MeshBus | RabbitMQ |
|---------|----------|
| Topic | Routing Key → Queue (`meshbus.{topic}`) |
| Message.Id | BasicProperties.MessageId |
| Message.Headers | BasicProperties.Headers |
| Message.CorrelationId | BasicProperties.CorrelationId |
| PublishAsync | BasicPublishAsync (via exchange) |
| SubscribeAsync | BasicConsumeAsync (com AsyncEventingBasicConsumer) |

---

### Azure Service Bus

```csharp
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.AzureServiceBus.DependencyInjection;

// Modo Queue
services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://meu-namespace.servicebus.windows.net/;...";
    opts.MaxConcurrentCalls = 5;                    // Processamento paralelo
    opts.AutoCompleteMessages = true;               // Auto-complete de mensagens
}));

// Modo Topic + Subscription
services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://meu-namespace.servicebus.windows.net/;...";
    opts.SubscriptionName = "minha-subscription";   // Nome da subscription
    opts.MaxConcurrentCalls = 10;
}));
```

**Mapeamento de conceitos:**
| MeshBus | Azure Service Bus |
|---------|-------------------|
| Topic | Queue ou Topic name |
| Message.Id | ServiceBusMessage.MessageId |
| Message.Headers | ApplicationProperties |
| Message.CorrelationId | ServiceBusMessage.CorrelationId |
| PublishAsync | SendMessageAsync |
| PublishBatchAsync | CreateMessageBatchAsync + SendMessagesAsync |
| SubscribeAsync | ServiceBusProcessor.StartProcessingAsync |

---

## 🔄 Guia de Migração

### Migrar de Kafka para RabbitMQ

**Antes (Kafka):**
```csharp
using Lib.MeshBus.Kafka.DependencyInjection;

services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "localhost:9092";
    opts.GroupId = "minha-app";
}));
```

**Depois (RabbitMQ):**
```csharp
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus => bus.UseRabbitMq(opts =>
{
    opts.HostName = "localhost";
    opts.UserName = "guest";
    opts.Password = "guest";
}));
```

**O que muda:** Apenas o `using` e a chamada `.UseKafka()` → `.UseRabbitMq()` com as opções do novo provider.

**O que NÃO muda:** Absolutamente nada no código de publicação e consumo. `IMeshBusPublisher`, `IMeshBusSubscriber`, `MeshBusMessage<T>` — tudo permanece idêntico.

---

### Migrar de RabbitMQ para Azure Service Bus

**Antes (RabbitMQ):**
```csharp
using Lib.MeshBus.RabbitMQ.DependencyInjection;

services.AddMeshBus(bus => bus.UseRabbitMq(opts =>
{
    opts.HostName = "localhost";
}));
```

**Depois (Azure Service Bus):**
```csharp
using Lib.MeshBus.AzureServiceBus.DependencyInjection;

services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://meu-namespace.servicebus.windows.net/;...";
    opts.SubscriptionName = "minha-subscription"; // necessário para topics
}));
```

---

### Migrar de Azure Service Bus para Kafka

**Antes (Azure Service Bus):**
```csharp
using Lib.MeshBus.AzureServiceBus.DependencyInjection;

services.AddMeshBus(bus => bus.UseAzureServiceBus(opts =>
{
    opts.ConnectionString = "Endpoint=sb://...";
}));
```

**Depois (Kafka):**
```csharp
using Lib.MeshBus.Kafka.DependencyInjection;

services.AddMeshBus(bus => bus.UseKafka(opts =>
{
    opts.BootstrapServers = "kafka-cluster:9092";
    opts.GroupId = "minha-app";
}));
```

---

### Checklist de Migração

1. ✅ Alterar o pacote NuGet (`dotnet remove package Lib.MeshBus.Kafka` → `dotnet add package Lib.MeshBus.RabbitMQ`)
2. ✅ Alterar o `using` no arquivo de configuração
3. ✅ Alterar `.UseKafka(...)` para `.UseRabbitMq(...)` com as opções do novo provider
4. ✅ Certificar-se de que tópicos/filas existem no novo broker (ou usar auto-create)
5. ✅ **Nenhuma alteração** nos controllers, services ou consumers

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

### MeshBusMessage\<T\>

```csharp
public class MeshBusMessage<T>
{
    string Id { get; set; }                          // GUID auto-gerado
    DateTimeOffset Timestamp { get; set; }           // UTC auto-gerado
    Dictionary<string, string> Headers { get; set; } // Custom headers
    T Body { get; set; }                             // Payload
    string? CorrelationId { get; set; }              // Rastreamento
    string Topic { get; set; }                       // Tópico/fila

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

Para usar um serializer customizado, registre-o antes de chamar `AddMeshBus()`:

```csharp
services.AddSingleton<IMessageSerializer, MeuProtobufSerializer>();
services.AddMeshBus(bus => bus.UseKafka(...));
```

---

## Tabela de Compatibilidade

| Feature | Kafka | RabbitMQ | Azure Service Bus |
|---------|:-----:|:--------:|:-----------------:|
| PublishAsync | ✅ | ✅ | ✅ |
| PublishBatchAsync | ✅ | ✅ | ✅ (native batch) |
| SubscribeAsync | ✅ | ✅ | ✅ |
| UnsubscribeAsync | ✅ | ✅ | ✅ |
| Headers/Metadata | ✅ | ✅ | ✅ |
| CorrelationId | ✅ | ✅ (native) | ✅ (native) |
| Durable Messages | ✅ | ✅ | ✅ |
| Serialização Custom | ✅ | ✅ | ✅ |
| IAsyncDisposable | ✅ | ✅ | ✅ |

---

## Roadmap

### ✅ Fase 1 — MVP (Atual)
- [x] Apache Kafka
- [x] RabbitMQ
- [x] Azure Service Bus

### ⬜ Fase 2 — Cloud Providers
- [ ] Azure Event Hubs
- [ ] Azure Event Grid
- [ ] AWS SQS
- [ ] AWS SNS
- [ ] AWS EventBridge
- [ ] Google Pub/Sub
- [ ] Google Cloud Tasks

### ⬜ Fase 3 — Enterprise & Specialized
- [ ] ActiveMQ
- [ ] IBM MQ
- [ ] HiveMQ (MQTT)
- [ ] Apache Pulsar

---

## Estrutura do Projeto

```
Lib.MeshBus/
├── Lib.MeshBus/                    # Core — interfaces e abstrações
│   ├── Abstractions/
│   │   ├── IMeshBusPublisher.cs
│   │   ├── IMeshBusSubscriber.cs
│   │   └── IMessageSerializer.cs
│   ├── Configuration/
│   │   ├── MeshBusOptions.cs
│   │   ├── KafkaOptions.cs
│   │   ├── RabbitMqOptions.cs
│   │   └── AzureServiceBusOptions.cs
│   ├── DependencyInjection/
│   │   ├── MeshBusBuilder.cs
│   │   └── ServiceCollectionExtensions.cs
│   ├── Exceptions/
│   │   └── MeshBusException.cs
│   ├── Models/
│   │   └── MeshBusMessage.cs
│   └── Serialization/
│       └── SystemTextJsonSerializer.cs
├── Lib.MeshBus.Kafka/              # Provider Kafka
├── Lib.MeshBus.RabbitMQ/           # Provider RabbitMQ
├── Lib.MeshBus.AzureServiceBus/    # Provider Azure Service Bus
├── Lib.MeshBus.Tests/              # Testes unitários (125 tests)
├── PRD.md                          # Product Requirements Document
├── PROGRESS.md                     # Progresso do projeto
└── README.md                       # Documentação
```

---

## Contribuindo

1. Fork o repositório
2. Crie uma branch: `git checkout -b feature/nova-feature`
3. Faça suas alterações
4. Execute os testes: `dotnet test`
5. Abra um Pull Request

---

## Licença

MIT License — veja [LICENSE](LICENSE) para detalhes.

