using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.EventGrid;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.EventGrid;

public class EventGridSubscriberTests
{
    private readonly EventGridReceiverClient _mockReceiverClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly EventGridOptions _options;
    private readonly EventGridSubscriber _subscriber;

    public EventGridSubscriberTests()
    {
        _mockReceiverClient = Substitute.For<EventGridReceiverClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new EventGridOptions
        {
            MaxEvents = 10,
            MaxWaitTimeSeconds = 1
        };
        _subscriber = new EventGridSubscriber(_mockReceiverClient, _mockSerializer, _options);
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
    public async Task SubscribeAsync_ShouldThrow_WhenAlreadySubscribed()
    {
        _mockReceiverClient.ReceiveAsync(
            Arg.Any<int?>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-topic", handler);

        var ex = Assert.Throws<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-topic", handler).GetAwaiter().GetResult());

        Assert.Equal("EventGrid", ex.Provider);
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
        await _subscriber.UnsubscribeAsync("not-subscribed");
    }
}
