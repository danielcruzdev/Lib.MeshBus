using Azure;
using Azure.Messaging.EventGrid;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.EventGrid;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.EventGrid;

public class EventGridPublisherTests
{
    private readonly EventGridPublisherClient _mockClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly EventGridPublisher _publisher;

    public EventGridPublisherTests()
    {
        _mockClient = Substitute.For<EventGridPublisherClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _publisher = new EventGridPublisher(_mockClient, _mockSerializer);
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockClient.SendEventAsync(Arg.Any<EventGridEvent>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldCallSendEventAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockClient.SendEventAsync(Arg.Any<EventGridEvent>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());

        await _publisher.PublishAsync(message);

        await _mockClient.Received(1).SendEventAsync(
            Arg.Any<EventGridEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnRequestFailedException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockClient.SendEventAsync(Arg.Any<EventGridEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException("Connection refused"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("EventGrid", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetEventId()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        EventGridEvent? captured = null;
        _mockClient.SendEventAsync(
            Arg.Do<EventGridEvent>(e => captured = e),
            Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Equal(message.Id, captured.Id);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldCallSendEventsAsync()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("Hello", "test-topic"),
            MeshBusMessage<string>.Create("World", "test-topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);
        _mockClient.SendEventsAsync(Arg.Any<IEnumerable<EventGridEvent>>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response>());

        await _publisher.PublishBatchAsync(messages);

        await _mockClient.Received(1).SendEventsAsync(
            Arg.Any<IEnumerable<EventGridEvent>>(),
            Arg.Any<CancellationToken>());
    }
}
