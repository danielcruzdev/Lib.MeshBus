using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.EventHubs;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.EventHubs;

public class EventHubsPublisherTests
{
    private readonly EventHubProducerClient _mockProducer;
    private readonly IMessageSerializer _mockSerializer;
    private readonly EventHubsPublisher _publisher;

    public EventHubsPublisherTests()
    {
        _mockProducer = Substitute.For<EventHubProducerClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        Func<string, EventHubProducerClient> factory = _ => _mockProducer;
        _publisher = new EventHubsPublisher(factory, _mockSerializer);
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-hub");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldCallSendAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-hub");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        await _mockProducer.Received(1).SendAsync(
            Arg.Any<IEnumerable<EventData>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnEventHubsException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-hub");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockProducer
            .SendAsync(Arg.Any<IEnumerable<EventData>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EventHubsException(false, "test-hub", "Service unavailable"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("EventHubs", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetMessageProperties()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-hub");
        message.Headers["x-custom"] = "value1";
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        IEnumerable<EventData>? capturedEvents = null;
        await _mockProducer
            .SendAsync(
                Arg.Do<IEnumerable<EventData>>(e => capturedEvents = e),
                Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedEvents);
        var eventData = capturedEvents.First();
        Assert.Equal(message.Id, eventData.Properties["meshbus.id"]);
        Assert.Equal("test-hub", eventData.Properties["meshbus.topic"]);
        Assert.Equal("value1", eventData.Properties["meshbus.header.x-custom"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldReuseProducerForSameTopic()
    {
        int factoryCalls = 0;
        Func<string, EventHubProducerClient> countingFactory = _ =>
        {
            factoryCalls++;
            return _mockProducer;
        };
        var publisher = new EventHubsPublisher(countingFactory, _mockSerializer);
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await publisher.PublishAsync(MeshBusMessage<string>.Create("A", "hub-a"));
        await publisher.PublishAsync(MeshBusMessage<string>.Create("B", "hub-a"));

        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task PublishAsync_ShouldCreateSeparateProducerPerTopic()
    {
        int factoryCalls = 0;
        Func<string, EventHubProducerClient> countingFactory = _ =>
        {
            factoryCalls++;
            return _mockProducer;
        };
        var publisher = new EventHubsPublisher(countingFactory, _mockSerializer);
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await publisher.PublishAsync(MeshBusMessage<string>.Create("A", "hub-a"));
        await publisher.PublishAsync(MeshBusMessage<string>.Create("B", "hub-b"));

        Assert.Equal(2, factoryCalls);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldSetCorrelationId()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-hub", correlationId: "corr-123");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        IEnumerable<EventData>? capturedEvents = null;
        await _mockProducer
            .SendAsync(
                Arg.Do<IEnumerable<EventData>>(e => capturedEvents = e),
                Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(capturedEvents);
        var eventData = capturedEvents.First();
        Assert.Equal("corr-123", eventData.Properties["meshbus.correlationId"]);
    }
}
