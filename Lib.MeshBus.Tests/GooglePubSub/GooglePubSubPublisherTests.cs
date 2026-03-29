using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.GooglePubSub;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.GooglePubSub;

public class GooglePubSubPublisherTests
{
    private readonly PublisherServiceApiClient _mockPublisherApi;
    private readonly IMessageSerializer _mockSerializer;
    private readonly GooglePubSubOptions _options;
    private readonly GooglePubSubPublisher _publisher;

    public GooglePubSubPublisherTests()
    {
        _mockPublisherApi = Substitute.For<PublisherServiceApiClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new GooglePubSubOptions
        {
            ProjectId = "test-project",
            AutoCreateResources = false
        };

        _mockPublisherApi
            .PublishAsync(Arg.Any<TopicName>(), Arg.Any<IEnumerable<PubsubMessage>>())
            .Returns(new PublishResponse { MessageIds = { "msg-1" } });

        _publisher = new GooglePubSubPublisher(_mockPublisherApi, _mockSerializer, _options);
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
    public async Task PublishAsync_ShouldCallPublishAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        await _mockPublisherApi.Received(1).PublishAsync(
            Arg.Any<TopicName>(),
            Arg.Any<IEnumerable<PubsubMessage>>());
    }

    [Fact]
    public async Task PublishAsync_ShouldSetCorrectTopicName()
    {
        var message = MeshBusMessage<string>.Create("Hello", "orders");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        TopicName? capturedTopic = null;
        _mockPublisherApi
            .PublishAsync(
                Arg.Do<TopicName>(t => capturedTopic = t),
                Arg.Any<IEnumerable<PubsubMessage>>())
            .Returns(new PublishResponse { MessageIds = { "msg-1" } });

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedTopic);
        Assert.Equal("test-project", capturedTopic.ProjectId);
        Assert.Equal("orders", capturedTopic.TopicId);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetMessageAttributes()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic", correlationId: "corr-1");
        message.Headers["x-source"] = "unit-test";
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        IEnumerable<PubsubMessage>? capturedMessages = null;
        _mockPublisherApi
            .PublishAsync(
                Arg.Any<TopicName>(),
                Arg.Do<IEnumerable<PubsubMessage>>(m => capturedMessages = m))
            .Returns(new PublishResponse { MessageIds = { "msg-1" } });

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedMessages);
        var pubsubMsg = capturedMessages.First();
        Assert.Equal(message.Id, pubsubMsg.Attributes["meshbus.id"]);
        Assert.Equal("test-topic", pubsubMsg.Attributes["meshbus.topic"]);
        Assert.Equal("corr-1", pubsubMsg.Attributes["meshbus.correlationId"]);
        Assert.Equal("unit-test", pubsubMsg.Attributes["meshbus.header.x-source"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnRpcException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockPublisherApi
            .PublishAsync(Arg.Any<TopicName>(), Arg.Any<IEnumerable<PubsubMessage>>())
            .ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "Service unavailable")));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("GooglePubSub", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldNotCallCreateTopic_WhenAutoCreateResourcesIsDisabled()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        await _mockPublisherApi.DidNotReceive().CreateTopicAsync(Arg.Any<TopicName>());
    }

    [Fact]
    public async Task PublishAsync_ShouldCallCreateTopic_WhenAutoCreateResourcesIsEnabled()
    {
        var options = new GooglePubSubOptions
        {
            ProjectId = "test-project",
            AutoCreateResources = true
        };
        var publisher = new GooglePubSubPublisher(_mockPublisherApi, _mockSerializer, options);

        _mockPublisherApi
            .CreateTopicAsync(Arg.Any<TopicName>())
            .Returns(new Topic { Name = "projects/test-project/topics/test-topic" });

        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await publisher.PublishAsync(message);

        await _mockPublisherApi.Received(1).CreateTopicAsync(
            Arg.Is<TopicName>(t => t.TopicId == "test-topic"));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }
}
