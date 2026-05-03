using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.GooglePubSub;
using Lib.MeshBus.Models;
using NSubstitute;

namespace Lib.MeshBus.Tests.GooglePubSub;

public class GooglePubSubSubscriberTests
{
    private readonly SubscriberServiceApiClient _mockSubscriberApi;
    private readonly PublisherServiceApiClient _mockPublisherApi;
    private readonly IMessageSerializer _mockSerializer;
    private readonly GooglePubSubOptions _options;
    private readonly GooglePubSubSubscriber _subscriber;

    public GooglePubSubSubscriberTests()
    {
        _mockSubscriberApi = Substitute.For<SubscriberServiceApiClient>();
        _mockPublisherApi = Substitute.For<PublisherServiceApiClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new GooglePubSubOptions
        {
            ProjectId = "test-project",
            SubscriptionSuffix = "meshbus-sub",
            AutoCreateResources = false,
            MaxMessages = 10
        };

        _subscriber = new GooglePubSubSubscriber(
            _mockSubscriberApi,
            _mockPublisherApi,
            _mockSerializer,
            _options);
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
    public async Task SubscribeAsync_ShouldThrow_WhenAlreadySubscribedToSameTopic()
    {
        // Return empty pull response to prevent tight loop
        _mockSubscriberApi.PullAsync(Arg.Any<SubscriptionName>(), Arg.Any<int>())
            .Returns(new PullResponse());

        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-topic", handler);

        var ex = Assert.Throws<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-topic", handler).GetAwaiter().GetResult());

        Assert.Equal("GooglePubSub", ex.Provider);
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
        await _subscriber.UnsubscribeAsync("non-existent");
    }

    [Fact]
    public async Task SubscribeAsync_ShouldInvokeHandlerAndAck_WhenMessagesAreReceived()
    {
        var received = new List<MeshBusMessage<string>>();

        var pubsubMessage = new PubsubMessage
        {
            MessageId = "gps-1",
            Data = ByteString.CopyFrom([10, 20, 30])
        };
        pubsubMessage.Attributes["meshbus.id"] = "test-id";
        pubsubMessage.Attributes["meshbus.topic"] = "test-topic";
        pubsubMessage.Attributes["meshbus.timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        var callCount = 0;
        _mockSubscriberApi.PullAsync(Arg.Any<SubscriptionName>(), Arg.Any<int>())
            .Returns(_ =>
            {
                if (callCount++ == 0)
                {
                    var pullResponse = new PullResponse();
                    pullResponse.ReceivedMessages.Add(new ReceivedMessage
                    {
                        AckId = "ack-1",
                        Message = pubsubMessage
                    });
                    return Task.FromResult(pullResponse);
                }
                return Task.FromResult(new PullResponse());
            });

        _mockSubscriberApi.AcknowledgeAsync(Arg.Any<SubscriptionName>(), Arg.Any<IEnumerable<string>>())
            .Returns(Task.CompletedTask);

        _mockSerializer.Deserialize<string>(Arg.Any<byte[]>()).Returns("Hello");

        await _subscriber.SubscribeAsync<string>("test-topic", msg =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        });

        // Use a polling loop instead of a fixed delay to avoid CI timing flakiness
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (received.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Single(received);
        Assert.Equal("test-id", received[0].Id);

        await _mockSubscriberApi.Received(1).AcknowledgeAsync(
            Arg.Any<SubscriptionName>(),
            Arg.Is<IEnumerable<string>>(ids => ids.Contains("ack-1")));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldUseDerivedSubscriptionName()
    {
        _mockSubscriberApi.PullAsync(Arg.Any<SubscriptionName>(), Arg.Any<int>())
            .Returns(new PullResponse());

        await _subscriber.SubscribeAsync<string>("orders", _ => Task.CompletedTask);

        await Task.Delay(100);

        await _mockSubscriberApi.Received().PullAsync(
            Arg.Is<SubscriptionName>(s =>
                s.ProjectId == "test-project" &&
                s.SubscriptionId == "orders-meshbus-sub"),
            Arg.Any<int>());
    }
}
