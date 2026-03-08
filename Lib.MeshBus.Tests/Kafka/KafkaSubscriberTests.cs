using System.Text;
using Confluent.Kafka;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Kafka;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.Kafka;

public class KafkaSubscriberTests
{
    private readonly IConsumer<string, byte[]> _mockConsumer;
    private readonly IMessageSerializer _mockSerializer;
    private readonly KafkaSubscriber _subscriber;

    public KafkaSubscriberTests()
    {
        _mockConsumer = Substitute.For<IConsumer<string, byte[]>>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _subscriber = new KafkaSubscriber(_mockConsumer, _mockSerializer);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCallConsumerSubscribe()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        handler.Invoke(Arg.Any<MeshBusMessage<string>>()).Returns(Task.CompletedTask);

        // Make Consume block until cancelled
        _mockConsumer.Consume(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                var ct = x.Arg<CancellationToken>();
                ct.WaitHandle.WaitOne(100);
                ct.ThrowIfCancellationRequested();
                return (ConsumeResult<string, byte[]>)null!;
            });

        await _subscriber.SubscribeAsync("test-topic", handler);

        _mockConsumer.Received(1).Subscribe("test-topic");

        // Cleanup
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

        _mockConsumer.Consume(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().WaitHandle.WaitOne(100);
                x.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return (ConsumeResult<string, byte[]>)null!;
            });

        await _subscriber.SubscribeAsync("test-topic", handler);

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-topic", handler));

        Assert.Equal("Kafka", ex.Provider);
        Assert.Contains("Already subscribed", ex.Message);

        // Cleanup
        await _subscriber.UnsubscribeAsync("test-topic");
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldCallConsumerUnsubscribe()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        _mockConsumer.Consume(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                x.Arg<CancellationToken>().WaitHandle.WaitOne(100);
                x.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return (ConsumeResult<string, byte[]>)null!;
            });

        await _subscriber.SubscribeAsync("test-topic", handler);
        await _subscriber.UnsubscribeAsync("test-topic");

        _mockConsumer.Received().Unsubscribe();
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldThrow_WhenTopicIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.UnsubscribeAsync(null!));
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldNotThrow_WhenNotSubscribed()
    {
        // Should be a no-op when not subscribed
        await _subscriber.UnsubscribeAsync("nonexistent-topic");
    }

    [Fact]
    public async Task DisposeAsync_ShouldCloseAndDisposeConsumer()
    {
        await _subscriber.DisposeAsync();

        _mockConsumer.Received(1).Close();
        _mockConsumer.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        await _subscriber.DisposeAsync();
        await _subscriber.DisposeAsync();

        _mockConsumer.Received(1).Close();
        _mockConsumer.Received(1).Dispose();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenConsumerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KafkaSubscriber((IConsumer<string, byte[]>)null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new KafkaSubscriber(_mockConsumer, null!));
    }
}

