using Azure.Messaging.ServiceBus;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.AzureServiceBus;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.AzureServiceBus;

public class AzureServiceBusPublisherTests
{
    private readonly ServiceBusClient _mockClient;
    private readonly ServiceBusSender _mockSender;
    private readonly IMessageSerializer _mockSerializer;
    private readonly AzureServiceBusPublisher _publisher;

    public AzureServiceBusPublisherTests()
    {
        _mockClient = Substitute.For<ServiceBusClient>();
        _mockSender = Substitute.For<ServiceBusSender>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        _mockClient.CreateSender(Arg.Any<string>()).Returns(_mockSender);
        _publisher = new AzureServiceBusPublisher(_mockClient, _mockSerializer);
    }

    [Fact]
    public async Task PublishAsync_ShouldCreateSenderForTopic()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        _mockClient.Received(1).CreateSender("test-topic");
    }

    [Fact]
    public async Task PublishAsync_ShouldCallSendMessageAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        await _mockSender.Received(1).SendMessageAsync(
            Arg.Any<ServiceBusMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldCacheSenderPerTopic()
    {
        var message1 = MeshBusMessage<string>.Create("Hello1", "topic-1");
        var message2 = MeshBusMessage<string>.Create("Hello2", "topic-1");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishAsync(message1);
        await _publisher.PublishAsync(message2);

        // Should only create sender once for the same topic
        _mockClient.Received(1).CreateSender("topic-1");
    }

    [Fact]
    public async Task PublishAsync_ShouldCreateDifferentSendersForDifferentTopics()
    {
        var message1 = MeshBusMessage<string>.Create("Hello1", "topic-1");
        var message2 = MeshBusMessage<string>.Create("Hello2", "topic-2");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishAsync(message1);
        await _publisher.PublishAsync(message2);

        _mockClient.Received(1).CreateSender("topic-1");
        _mockClient.Received(1).CreateSender("topic-2");
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnServiceBusException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockSender.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ServiceBusException("connection failed", ServiceBusFailureReason.ServiceBusy));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("AzureServiceBus", ex.Provider);
        Assert.Contains("test-topic", ex.Message);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldNotThrow_WhenMessagesIsEmpty()
    {
        await _publisher.PublishBatchAsync(Array.Empty<MeshBusMessage<string>>());

        // Should be a no-op
        _mockClient.DidNotReceive().CreateSender(Arg.Any<string>());
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeSendersAndClient()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);
        await _publisher.DisposeAsync();

        await _mockSender.Received(1).DisposeAsync();
        await _mockClient.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        await _publisher.DisposeAsync();
        await _publisher.DisposeAsync();

        await _mockClient.Received(1).DisposeAsync();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureServiceBusPublisher(null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureServiceBusPublisher(_mockClient, null!));
    }
}

