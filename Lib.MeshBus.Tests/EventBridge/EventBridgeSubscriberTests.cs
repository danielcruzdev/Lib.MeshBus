using Amazon.EventBridge;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.EventBridge;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;

namespace Lib.MeshBus.Tests.EventBridge;

public class EventBridgeSubscriberTests
{
    private readonly IAmazonEventBridge _mockEb;
    private readonly IAmazonSQS _mockSqs;
    private readonly IMessageSerializer _mockSerializer;
    private readonly EventBridgeOptions _options;
    private readonly EventBridgeSubscriber _subscriber;

    public EventBridgeSubscriberTests()
    {
        _mockEb = Substitute.For<IAmazonEventBridge>();
        _mockSqs = Substitute.For<IAmazonSQS>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new EventBridgeOptions
        {
            EventBusName = "test-bus",
            Source = "meshbus",
            AutoCreateSqsTarget = false,
            WaitTimeSeconds = 0,
            MaxNumberOfMessages = 1
        };

        _mockSqs.CreateQueueAsync(Arg.Any<CreateQueueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateQueueResponse { QueueUrl = "http://localhost:9324/000000000000/test-queue" });

        _subscriber = new EventBridgeSubscriber(_mockEb, _mockSqs, _mockSerializer, _options);
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
        _mockSqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReceiveMessageResponse { Messages = [] });

        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-topic", handler);

        var ex = Assert.Throws<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-topic", handler).GetAwaiter().GetResult());

        Assert.Equal("EventBridge", ex.Provider);
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
