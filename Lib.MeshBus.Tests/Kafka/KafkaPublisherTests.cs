using System.Text;
using Confluent.Kafka;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Kafka;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.Kafka;

public class KafkaPublisherTests
{
    private readonly IProducer<string, byte[]> _mockProducer;
    private readonly IMessageSerializer _mockSerializer;
    private readonly KafkaPublisher _publisher;

    public KafkaPublisherTests()
    {
        _mockProducer = Substitute.For<IProducer<string, byte[]>>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _publisher = new KafkaPublisher(_mockProducer, _mockSerializer);
    }

    [Fact]
    public async Task PublishAsync_ShouldCallProducerWithCorrectTopic()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns(Encoding.UTF8.GetBytes("\"Hello\""));

        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, byte[]>());

        await _publisher.PublishAsync(message);

        await _mockProducer.Received(1).ProduceAsync(
            "test-topic",
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeMessageBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns(Encoding.UTF8.GetBytes("\"Hello\""));

        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, byte[]>());

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldSetMessageKey()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        Message<string, byte[]>? capturedMessage = null;
        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Do<Message<string, byte[]>>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, byte[]>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMessage);
        Assert.Equal(message.Id, capturedMessage.Key);
    }

    [Fact]
    public async Task PublishAsync_ShouldMapHeaders()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        message.Headers["custom-header"] = "custom-value";
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        Message<string, byte[]>? capturedMessage = null;
        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Do<Message<string, byte[]>>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, byte[]>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMessage);
        var headerValue = capturedMessage.Headers
            .FirstOrDefault(h => h.Key == "custom-header");
        Assert.NotNull(headerValue);
        Assert.Equal("custom-value", Encoding.UTF8.GetString(headerValue.GetValueBytes()));
    }

    [Fact]
    public async Task PublishAsync_ShouldMapCorrelationId()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic", "corr-123");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        Message<string, byte[]>? capturedMessage = null;
        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Do<Message<string, byte[]>>(m => capturedMessage = m),
            Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, byte[]>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMessage);
        var correlationHeader = capturedMessage.Headers
            .FirstOrDefault(h => h.Key == "meshbus-correlation-id");
        Assert.NotNull(correlationHeader);
        Assert.Equal("corr-123", Encoding.UTF8.GetString(correlationHeader.GetValueBytes()));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_WhenProducerFails()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProduceException<string, byte[]>(
                new Error(ErrorCode.BrokerNotAvailable, "Broker not available"),
                new DeliveryResult<string, byte[]>()));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("Kafka", ex.Provider);
        Assert.Contains("test-topic", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllMessages()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("Msg1", "test-topic"),
            MeshBusMessage<string>.Create("Msg2", "test-topic"),
            MeshBusMessage<string>.Create("Msg3", "test-topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>())
            .Returns(new DeliveryResult<string, byte[]>());

        await _publisher.PublishBatchAsync(messages);

        await _mockProducer.Received(3).ProduceAsync(
            "test-topic",
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldCollectErrors_AndThrowAggregate()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("Msg1", "test-topic"),
            MeshBusMessage<string>.Create("Msg2", "test-topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        _mockProducer.ProduceAsync(
            Arg.Any<string>(),
            Arg.Any<Message<string, byte[]>>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProduceException<string, byte[]>(
                new Error(ErrorCode.BrokerNotAvailable, "fail"),
                new DeliveryResult<string, byte[]>()));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishBatchAsync(messages));

        Assert.Equal("Kafka", ex.Provider);
        Assert.IsType<AggregateException>(ex.InnerException);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeProducer()
    {
        await _publisher.DisposeAsync();

        _mockProducer.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        await _publisher.DisposeAsync();
        await _publisher.DisposeAsync();

        _mockProducer.Received(1).Dispose();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenProducerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KafkaPublisher((IProducer<string, byte[]>)null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KafkaPublisher(_mockProducer, null!));
    }
}

