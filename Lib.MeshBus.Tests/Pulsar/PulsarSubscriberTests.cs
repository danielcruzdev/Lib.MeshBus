using System.Buffers;
using DotPulsar;
using DotPulsar.Abstractions;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Pulsar;
using NSubstitute;

namespace Lib.MeshBus.Tests.Pulsar;

public class PulsarSubscriberTests
{
    private readonly IPulsarClient _mockClient;
    private readonly IConsumer<ReadOnlySequence<byte>> _mockConsumer;
    private readonly IMessageSerializer _mockSerializer;
    private readonly PulsarSubscriber _subscriber;

    public PulsarSubscriberTests()
    {
        _mockClient = Substitute.For<IPulsarClient>();
        _mockConsumer = Substitute.For<IConsumer<ReadOnlySequence<byte>>>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        _mockClient.CreateConsumer(Arg.Any<ConsumerOptions<ReadOnlySequence<byte>>>())
            .Returns(_mockConsumer);

        // Receive blocks until the cancellation token is cancelled, then throws OCE
        _mockConsumer.Receive(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ct = ci.ArgAt<CancellationToken>(0);
                return new ValueTask<IMessage<ReadOnlySequence<byte>>>(
                    Task.Delay(Timeout.Infinite, ct)
                        .ContinueWith<IMessage<ReadOnlySequence<byte>>>(
                            _ => throw new OperationCanceledException(),
                            TaskScheduler.Default));
            });

        _subscriber = new PulsarSubscriber(_mockClient, _mockSerializer);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCreateConsumerForTopic()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test-topic", handler);
        await Task.Delay(50); // allow Task.Run loop to start and call CreateConsumer

        _mockClient.Received(1).CreateConsumer(
            Arg.Is<ConsumerOptions<ReadOnlySequence<byte>>>(o => o.Topic == "test-topic"));

        await _subscriber.UnsubscribeAsync("test-topic");
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrow_WhenTopicIsNull()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.SubscribeAsync(null!, handler));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrow_WhenTopicIsEmpty()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _subscriber.SubscribeAsync("", handler));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrow_WhenHandlerIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.SubscribeAsync<string>("test-topic", null!));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrowMeshBusException_WhenAlreadySubscribed()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test-topic", handler);
        await Task.Delay(20);

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-topic", handler));

        Assert.Equal("Pulsar", ex.Provider);
        Assert.Contains("Already subscribed", ex.Message);

        await _subscriber.UnsubscribeAsync("test-topic");
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldNotThrow_WhenNotSubscribed()
    {
        // No-op when not subscribed
        await _subscriber.UnsubscribeAsync("nonexistent-topic");
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldThrow_WhenTopicIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.UnsubscribeAsync(null!));
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldThrow_WhenTopicIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _subscriber.UnsubscribeAsync(""));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PulsarSubscriber((IPulsarClient)null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PulsarSubscriber(_mockClient, null!));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        await _subscriber.DisposeAsync();
        await _subscriber.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAsync_ShouldConfigureSubscriptionType_Shared()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        var subscriber = new PulsarSubscriber(_mockClient, _mockSerializer, "sub-name", "Shared");

        await subscriber.SubscribeAsync("test-topic", handler);
        await Task.Delay(50);

        _mockClient.Received(1).CreateConsumer(
            Arg.Is<ConsumerOptions<ReadOnlySequence<byte>>>(o =>
                o.SubscriptionType == DotPulsar.SubscriptionType.Shared));

        await subscriber.UnsubscribeAsync("test-topic");
        await subscriber.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeAsync_ShouldConfigureSubscriptionName()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        var subscriber = new PulsarSubscriber(_mockClient, _mockSerializer, "my-sub");

        await subscriber.SubscribeAsync("test-topic", handler);
        await Task.Delay(50);

        _mockClient.Received(1).CreateConsumer(
            Arg.Is<ConsumerOptions<ReadOnlySequence<byte>>>(o =>
                o.SubscriptionName == "my-sub"));

        await subscriber.UnsubscribeAsync("test-topic");
        await subscriber.DisposeAsync();
    }
}
