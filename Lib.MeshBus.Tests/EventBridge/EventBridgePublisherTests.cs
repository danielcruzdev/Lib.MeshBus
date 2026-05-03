using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.EventBridge;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.EventBridge;

public class EventBridgePublisherTests
{
    private readonly IAmazonEventBridge _mockClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly EventBridgeOptions _options;
    private readonly EventBridgePublisher _publisher;

    public EventBridgePublisherTests()
    {
        _mockClient = Substitute.For<IAmazonEventBridge>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new EventBridgeOptions
        {
            EventBusName = "test-bus",
            Source = "meshbus"
        };
        _publisher = new EventBridgePublisher(_mockClient, _mockSerializer, _options);
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockClient.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [] });

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldCallPutEventsAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockClient.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [] });

        await _publisher.PublishAsync(message);

        await _mockClient.Received(1).PutEventsAsync(
            Arg.Any<PutEventsRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldSetCorrectDetailType()
    {
        var message = MeshBusMessage<string>.Create("Hello", "order-events");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        PutEventsRequest? captured = null;
        _mockClient.PutEventsAsync(
            Arg.Do<PutEventsRequest>(r => captured = r),
            Arg.Any<CancellationToken>())
            .Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [] });

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Single(captured.Entries);
        Assert.Equal("order-events", captured.Entries[0].DetailType);
        Assert.Equal("meshbus", captured.Entries[0].Source);
        Assert.Equal("test-bus", captured.Entries[0].EventBusName);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnEventBridgeException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockClient.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonEventBridgeException("Connection refused"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("EventBridge", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnFailedEntry()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockClient.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PutEventsResponse
            {
                FailedEntryCount = 1,
                Entries = [new PutEventsResultEntry { ErrorCode = "InternalFailure", ErrorMessage = "Something went wrong" }]
            });

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("EventBridge", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldEmbedMetadataInDetail()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic", correlationId: "corr-42");
        message.Headers["x-trace"] = "abc";
        _mockSerializer.Serialize("Hello").Returns([10, 20]);

        PutEventsRequest? captured = null;
        _mockClient.PutEventsAsync(
            Arg.Do<PutEventsRequest>(r => captured = r),
            Arg.Any<CancellationToken>())
            .Returns(new PutEventsResponse { FailedEntryCount = 0, Entries = [] });

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Contains("\"CorrelationId\":\"corr-42\"", captured.Entries[0].Detail);
        Assert.Contains("\"x-trace\":\"abc\"", captured.Entries[0].Detail);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }
}
