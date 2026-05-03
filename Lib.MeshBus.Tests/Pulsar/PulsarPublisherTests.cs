using System.Buffers;
using System.Text;
using DotPulsar;
using DotPulsar.Abstractions;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Pulsar;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.Pulsar;

public class PulsarPublisherTests
{
    private readonly IPulsarClient _mockClient;
    private readonly IProducer<ReadOnlySequence<byte>> _mockProducer;
    private readonly IMessageSerializer _mockSerializer;
    private readonly PulsarPublisher _publisher;

    public PulsarPublisherTests()
    {
        _mockClient = Substitute.For<IPulsarClient>();
        _mockProducer = Substitute.For<IProducer<ReadOnlySequence<byte>>>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        _mockClient.CreateProducer(Arg.Any<ProducerOptions<ReadOnlySequence<byte>>>())
            .Returns(_mockProducer);

        _mockProducer.Send(
                Arg.Any<MessageMetadata>(),
                Arg.Any<ReadOnlySequence<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MessageId>(MessageId.Earliest));

        _publisher = new PulsarPublisher(_mockClient, _mockSerializer);
    }

    [Fact]
    public async Task PublishAsync_ShouldSendMessageToCorrectTopic()
    {
        var message = MeshBusMessage<string>.Create("Hello", "persistent://public/default/orders");
        _mockSerializer.Serialize("Hello").Returns(Encoding.UTF8.GetBytes("\"Hello\""));

        await _publisher.PublishAsync(message);

        _mockClient.Received(1).CreateProducer(
            Arg.Is<ProducerOptions<ReadOnlySequence<byte>>>(o =>
                o.Topic == "persistent://public/default/orders"));
        await _mockProducer.Received(1).Send(
            Arg.Any<MessageMetadata>(),
            Arg.Any<ReadOnlySequence<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeMessageBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldSetMessageIdInMetadata()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MessageMetadata? capturedMetadata = null;
        await _mockProducer.Send(
            Arg.Do<MessageMetadata>(m => capturedMetadata = m),
            Arg.Any<ReadOnlySequence<byte>>(),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMetadata);
        Assert.Equal(message.Id, capturedMetadata["meshbus-message-id"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetCorrelationIdInMetadata()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic", "corr-42");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MessageMetadata? capturedMetadata = null;
        await _mockProducer.Send(
            Arg.Do<MessageMetadata>(m => capturedMetadata = m),
            Arg.Any<ReadOnlySequence<byte>>(),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMetadata);
        Assert.Equal("corr-42", capturedMetadata["meshbus-correlation-id"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldMapCustomHeadersToMetadata()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        message.Headers["x-custom"] = "value-123";
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MessageMetadata? capturedMetadata = null;
        await _mockProducer.Send(
            Arg.Do<MessageMetadata>(m => capturedMetadata = m),
            Arg.Any<ReadOnlySequence<byte>>(),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMetadata);
        Assert.Equal("value-123", capturedMetadata["x-custom"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldCacheProducerPerTopic()
    {
        var message = MeshBusMessage<string>.Create("Hello", "my-topic");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);
        await _publisher.PublishAsync(message);

        // CreateProducer for the same topic should only be called once
        _mockClient.Received(1).CreateProducer(
            Arg.Is<ProducerOptions<ReadOnlySequence<byte>>>(o => o.Topic == "my-topic"));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldWrapExceptionInMeshBusException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockProducer.Send(
                Arg.Any<MessageMetadata>(),
                Arg.Any<ReadOnlySequence<byte>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Broker down"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("Pulsar", ex.Provider);
        Assert.Contains("test-topic", ex.Message);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllMessages()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("M1", "batch-topic"),
            MeshBusMessage<string>.Create("M2", "batch-topic"),
            MeshBusMessage<string>.Create("M3", "batch-topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishBatchAsync(messages);

        await _mockProducer.Received(3).Send(
            Arg.Any<MessageMetadata>(),
            Arg.Any<ReadOnlySequence<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldCollectErrors_AndThrowMeshBusException()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("M1", "test-topic"),
            MeshBusMessage<string>.Create("M2", "test-topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        _mockProducer.Send(
                Arg.Any<MessageMetadata>(),
                Arg.Any<ReadOnlySequence<byte>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Send failed"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishBatchAsync(messages));

        Assert.Equal("Pulsar", ex.Provider);
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PulsarPublisher((IPulsarClient)null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PulsarPublisher(_mockClient, null!));
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAllProducers()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);
        await _publisher.PublishAsync(message);

        await _publisher.DisposeAsync();

        await _mockProducer.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);
        await _publisher.PublishAsync(message);

        await _publisher.DisposeAsync();
        await _publisher.DisposeAsync();

        await _mockProducer.Received(1).DisposeAsync();
    }
}