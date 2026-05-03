using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Sns;
using NSubstitute;

namespace Lib.MeshBus.Tests.SNS;

public class SnsSubscriberTests
{
    private readonly IAmazonSimpleNotificationService _mockSns;
    private readonly IAmazonSQS _mockSqs;
    private readonly IMessageSerializer _mockSerializer;
    private readonly SnsOptions _options;
    private readonly SnsTopicResolver _resolver;
    private readonly SnsSubscriber _subscriber;

    public SnsSubscriberTests()
    {
        _mockSns = Substitute.For<IAmazonSimpleNotificationService>();
        _mockSqs = Substitute.For<IAmazonSQS>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new SnsOptions
        {
            AutoCreateTopics = true,
            AutoCreateSqsSubscription = false,
            WaitTimeSeconds = 0,
            MaxNumberOfMessages = 1
        };

        _mockSns.CreateTopicAsync(Arg.Any<Amazon.SimpleNotificationService.Model.CreateTopicRequest>(), Arg.Any<CancellationToken>())
            .Returns(new Amazon.SimpleNotificationService.Model.CreateTopicResponse
            {
                TopicArn = "arn:aws:sns:us-east-1:000000000000:test-topic"
            });

        _mockSqs.CreateQueueAsync(Arg.Any<CreateQueueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateQueueResponse { QueueUrl = "http://localhost:9324/000000000000/test-queue" });

        _resolver = new SnsTopicResolver(_mockSns, _options);
        _subscriber = new SnsSubscriber(_mockSns, _mockSqs, _mockSerializer, _resolver, _options);
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

        Assert.Equal("SNS", ex.Provider);
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
