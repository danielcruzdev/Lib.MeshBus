using Azure.Messaging.EventHubs.Consumer;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.EventHubs;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;

namespace Lib.MeshBus.Tests.EventHubs;

public class EventHubsSubscriberTests
{
    private readonly EventHubConsumerClient _mockConsumer;
    private readonly IMessageSerializer _mockSerializer;
    private readonly EventHubsSubscriber _subscriber;

    public EventHubsSubscriberTests()
    {
        _mockConsumer = Substitute.For<EventHubConsumerClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        // Return an empty async stream by default so the consume loop terminates cleanly
        _mockConsumer
            .ReadEventsAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyAsyncEnumerable());

        Func<string, EventHubConsumerClient> factory = _ => _mockConsumer;
        _subscriber = new EventHubsSubscriber(factory, _mockSerializer);
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
            _subscriber.SubscribeAsync<string>("test-hub", null!));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrow_WhenAlreadySubscribedToSameTopic()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test-hub", handler);

        var ex = Assert.Throws<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-hub", handler).GetAwaiter().GetResult());

        Assert.Equal("EventHubs", ex.Provider);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCreateOneConsumerPerTopic()
    {
        int factoryCalls = 0;
        Func<string, EventHubConsumerClient> countingFactory = _ =>
        {
            factoryCalls++;
            return _mockConsumer;
        };
        var subscriber = new EventHubsSubscriber(countingFactory, _mockSerializer);

        await subscriber.SubscribeAsync<string>("hub-a", _ => Task.CompletedTask);
        await subscriber.SubscribeAsync<string>("hub-b", _ => Task.CompletedTask);

        Assert.Equal(2, factoryCalls);
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
    public async Task UnsubscribeAsync_ShouldBeNoOp_WhenNotSubscribed()
    {
        // Should not throw
        await _subscriber.UnsubscribeAsync("non-existent-topic");
    }

    private static async IAsyncEnumerable<PartitionEvent> EmptyAsyncEnumerable()
    {
        await Task.Yield();
        yield break;
    }
}

