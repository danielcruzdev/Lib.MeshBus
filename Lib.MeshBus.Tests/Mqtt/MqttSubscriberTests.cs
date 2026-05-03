using HiveMQtt.Client.Events;
using HiveMQtt.MQTT5.Types;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Mqtt;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.Mqtt;

public class MqttSubscriberTests
{
    private readonly IHiveMqttClient _mockClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly MqttSubscriber _subscriber;

    public MqttSubscriberTests()
    {
        _mockClient = Substitute.For<IHiveMqttClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _subscriber = new MqttSubscriber(_mockClient, _mockSerializer);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldConnectAndSubscribeToTopic()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test/topic", handler);

        await _mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await _mockClient.Received(1).SubscribeAsync(
            "test/topic",
            Arg.Any<QualityOfService>(),
            Arg.Any<CancellationToken>());
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
            _subscriber.SubscribeAsync<string>("test/topic", null!));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrowMeshBusException_WhenAlreadySubscribed()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test/topic", handler);

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test/topic", handler));

        Assert.Equal("MQTT", ex.Provider);
        Assert.Contains("Already subscribed", ex.Message);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldConnectOnlyOnce_ForMultipleTopics()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("topic/a", handler);
        await _subscriber.SubscribeAsync("topic/b", handler);

        await _mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await _mockClient.Received(2).SubscribeAsync(
            Arg.Any<string>(),
            Arg.Any<QualityOfService>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldUnsubscribeFromBroker()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test/topic", handler);

        await _subscriber.UnsubscribeAsync("test/topic");

        await _mockClient.Received(1).UnsubscribeAsync("test/topic", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldNotThrow_WhenNotSubscribed()
    {
        await _subscriber.UnsubscribeAsync("nonexistent/topic");
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
    public async Task OnMessageReceived_ShouldDispatchToCorrectHandler()
    {
        var received = new List<MeshBusMessage<string>>();
        var handler = (MeshBusMessage<string> msg) =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        };

        _mockSerializer.Deserialize<string>(Arg.Any<byte[]>()).Returns("hello");

        await _subscriber.SubscribeAsync("my/topic", handler);

        // Simulate incoming message by raising the event
        var mqttMessage = new MQTT5PublishMessage
        {
            Topic = "my/topic",
            Payload = [1, 2, 3],
            UserProperties = []
        };
        _mockClient.OnMessageReceived += Raise.EventWith(new OnMessageReceivedEventArgs(mqttMessage));

        await Task.Delay(50); // allow async dispatch

        Assert.Single(received);
        Assert.Equal("hello", received[0].Body);
        Assert.Equal("my/topic", received[0].Topic);
    }

    [Fact]
    public async Task OnMessageReceived_ShouldNotDispatch_WhenTopicNotSubscribed()
    {
        var received = new List<MeshBusMessage<string>>();
        var handler = (MeshBusMessage<string> msg) =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        };

        await _subscriber.SubscribeAsync("my/topic", handler);

        var mqttMessage = new MQTT5PublishMessage
        {
            Topic = "other/topic",
            Payload = [1, 2, 3]
        };
        _mockClient.OnMessageReceived += Raise.EventWith(new OnMessageReceivedEventArgs(mqttMessage));

        await Task.Delay(30);

        Assert.Empty(received);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MqttSubscriber((IHiveMqttClient)null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MqttSubscriber(_mockClient, null!));
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeClient()
    {
        await _subscriber.DisposeAsync();

        await _mockClient.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldDisposeOnce()
    {
        await _subscriber.DisposeAsync();
        await _subscriber.DisposeAsync();

        await _mockClient.Received(1).DisposeAsync();
    }
}
