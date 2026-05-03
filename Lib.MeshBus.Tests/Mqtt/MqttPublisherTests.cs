using System.Text;
using HiveMQtt.Client.Events;
using HiveMQtt.MQTT5.Types;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Mqtt;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.Mqtt;

public class MqttPublisherTests
{
    private readonly IHiveMqttClient _mockClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly MqttPublisher _publisher;

    public MqttPublisherTests()
    {
        _mockClient = Substitute.For<IHiveMqttClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _publisher = new MqttPublisher(_mockClient, _mockSerializer);
    }

    [Fact]
    public async Task PublishAsync_ShouldConnectOnFirstPublish()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        _mockSerializer.Serialize("Hello").Returns(Encoding.UTF8.GetBytes("\"Hello\""));

        await _publisher.PublishAsync(message);

        await _mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldConnectOnlyOnce_AcrossMultiplePublishes()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);
        await _publisher.PublishAsync(message);
        await _publisher.PublishAsync(message);

        await _mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await _mockClient.Received(3).PublishAsync(Arg.Any<MQTT5PublishMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldCallPublishWithCorrectTopic()
    {
        var message = MeshBusMessage<string>.Create("Hello", "sensors/temperature");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MQTT5PublishMessage? captured = null;
        await _mockClient.PublishAsync(
            Arg.Do<MQTT5PublishMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Equal("sensors/temperature", captured.Topic);
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeMessageBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldSetPayload()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        var expectedPayload = Encoding.UTF8.GetBytes("\"Hello\"");
        _mockSerializer.Serialize("Hello").Returns(expectedPayload);

        MQTT5PublishMessage? captured = null;
        await _mockClient.PublishAsync(
            Arg.Do<MQTT5PublishMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Equal(expectedPayload, captured.Payload);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetMessageIdInUserProperties()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MQTT5PublishMessage? captured = null;
        await _mockClient.PublishAsync(
            Arg.Do<MQTT5PublishMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured?.UserProperties);
        Assert.True(captured.UserProperties!.ContainsKey("meshbus-message-id"));
        Assert.Equal(message.Id, captured.UserProperties!["meshbus-message-id"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetCorrelationData()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic", "corr-99");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MQTT5PublishMessage? captured = null;
        await _mockClient.PublishAsync(
            Arg.Do<MQTT5PublishMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured?.CorrelationData);
        Assert.Equal("corr-99", Encoding.UTF8.GetString(captured.CorrelationData!));
    }

    [Fact]
    public async Task PublishAsync_ShouldMapCustomHeadersToUserProperties()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        message.Headers["x-tenant"] = "acme";
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        MQTT5PublishMessage? captured = null;
        await _mockClient.PublishAsync(
            Arg.Do<MQTT5PublishMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured?.UserProperties);
        Assert.True(captured.UserProperties!.ContainsKey("x-tenant"));
        Assert.Equal("acme", captured.UserProperties!["x-tenant"]);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldWrapExceptionInMeshBusException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test/topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockClient.PublishAsync(Arg.Any<MQTT5PublishMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection lost"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("MQTT", ex.Provider);
        Assert.Contains("test/topic", ex.Message);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllMessages()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("M1", "batch/topic"),
            MeshBusMessage<string>.Create("M2", "batch/topic"),
            MeshBusMessage<string>.Create("M3", "batch/topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishBatchAsync(messages);

        await _mockClient.Received(3).PublishAsync(
            Arg.Any<MQTT5PublishMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldCollectErrors_AndThrowMeshBusException()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("M1", "test/topic"),
            MeshBusMessage<string>.Create("M2", "test/topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        _mockClient.PublishAsync(Arg.Any<MQTT5PublishMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Publish failed"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishBatchAsync(messages));

        Assert.Equal("MQTT", ex.Provider);
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MqttPublisher((IHiveMqttClient)null!, _mockSerializer));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MqttPublisher(_mockClient, null!));
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeClient()
    {
        await _publisher.DisposeAsync();

        await _mockClient.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldDisposeOnce()
    {
        await _publisher.DisposeAsync();
        await _publisher.DisposeAsync();

        await _mockClient.Received(1).DisposeAsync();
    }
}
