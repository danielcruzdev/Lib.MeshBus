using System.Text;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.RabbitMQ;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;

namespace Lib.MeshBus.Tests.RabbitMQ;

public class RabbitMqPublisherTests
{
    private readonly IConnection _mockConnection;
    private readonly IChannel _mockChannel;
    private readonly IMessageSerializer _mockSerializer;
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqPublisher _publisher;

    public RabbitMqPublisherTests()
    {
        _mockConnection = Substitute.For<IConnection>();
        _mockChannel = Substitute.For<IChannel>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new RabbitMqOptions
        {
            ExchangeName = "test-exchange",
            ExchangeType = "topic",
            Durable = true,
            AutoDelete = false
        };
        _publisher = new RabbitMqPublisher(_mockConnection, _mockChannel, _mockSerializer, _options);
    }

    [Fact]
    public async Task PublishAsync_ShouldDeclareExchange()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        await _mockChannel.Received(1).ExchangeDeclareAsync(
            exchange: "test-exchange",
            type: "topic",
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldCallBasicPublishAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        await _publisher.PublishAsync(message);

        await _mockChannel.Received(1).BasicPublishAsync(
            exchange: "test-exchange",
            routingKey: "test-topic",
            mandatory: false,
            basicProperties: Arg.Any<BasicProperties>(),
            body: Arg.Any<ReadOnlyMemory<byte>>(),
            cancellationToken: Arg.Any<CancellationToken>());
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
    public async Task PublishAsync_ShouldDeclareExchangeOnlyOnce()
    {
        var message1 = MeshBusMessage<string>.Create("Hello1", "test-topic");
        var message2 = MeshBusMessage<string>.Create("Hello2", "test-topic");
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishAsync(message1);
        await _publisher.PublishAsync(message2);

        await _mockChannel.Received(1).ExchangeDeclareAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnFailure()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockChannel.ExchangeDeclareAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("connection lost"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("RabbitMQ", ex.Provider);
        Assert.Contains("test-topic", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllMessages()
    {
        var messages = new[]
        {
            MeshBusMessage<string>.Create("Msg1", "test-topic"),
            MeshBusMessage<string>.Create("Msg2", "test-topic")
        };
        _mockSerializer.Serialize(Arg.Any<string>()).Returns([1, 2, 3]);

        await _publisher.PublishBatchAsync(messages);

        await _mockChannel.Received(2).BasicPublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }

    [Fact]
    public async Task DisposeAsync_ShouldCloseChannelAndConnection()
    {
        var publisher = new RabbitMqPublisher(_mockConnection, _mockChannel, _mockSerializer, _options, ownsConnection: true);
        await publisher.DisposeAsync();

        await _mockChannel.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        _mockChannel.Received(1).Dispose();
        await _mockConnection.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        _mockConnection.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        var publisher = new RabbitMqPublisher(_mockConnection, _mockChannel, _mockSerializer, _options, ownsConnection: true);
        await publisher.DisposeAsync();
        await publisher.DisposeAsync();

        _mockChannel.Received(1).Dispose();
        _mockConnection.Received(1).Dispose();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenConnectionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqPublisher(null!, _mockChannel, _mockSerializer, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenChannelIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqPublisher(_mockConnection, null!, _mockSerializer, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqPublisher(_mockConnection, _mockChannel, null!, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqPublisher(_mockConnection, _mockChannel, _mockSerializer, null!));
    }
}

