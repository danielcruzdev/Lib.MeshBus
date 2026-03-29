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

public class RabbitMqSubscriberTests
{
    private readonly IConnection _mockConnection;
    private readonly IChannel _mockChannel;
    private readonly IMessageSerializer _mockSerializer;
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqSubscriber _subscriber;

    public RabbitMqSubscriberTests()
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
        _subscriber = new RabbitMqSubscriber(_mockConnection, _mockChannel, _mockSerializer, _options);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldDeclareExchange()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        _mockChannel.BasicConsumeAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>())
            .Returns("consumer-tag-1");

        await _subscriber.SubscribeAsync("test-topic", handler);

        await _mockChannel.Received(1).ExchangeDeclareAsync(
            exchange: "test-exchange",
            type: "topic",
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeAsync_ShouldDeclareQueue()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        _mockChannel.BasicConsumeAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>())
            .Returns("consumer-tag-1");

        await _subscriber.SubscribeAsync("test-topic", handler);

        await _mockChannel.Received(1).QueueDeclareAsync(
            queue: "meshbus.test-topic",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeAsync_ShouldBindQueueToExchange()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        _mockChannel.BasicConsumeAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>())
            .Returns("consumer-tag-1");

        await _subscriber.SubscribeAsync("test-topic", handler);

        await _mockChannel.Received(1).QueueBindAsync(
            queue: "meshbus.test-topic",
            exchange: "test-exchange",
            routingKey: "test-topic",
            arguments: null,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCallBasicConsumeAsync()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        _mockChannel.BasicConsumeAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>())
            .Returns("consumer-tag-1");

        await _subscriber.SubscribeAsync("test-topic", handler);

        await _mockChannel.Received(1).BasicConsumeAsync(
            queue: "meshbus.test-topic",
            autoAck: true,
            Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            consumer: Arg.Any<IAsyncBasicConsumer>(),
            cancellationToken: Arg.Any<CancellationToken>());
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
    public async Task SubscribeAsync_ShouldThrowMeshBusException_WhenAlreadySubscribed()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        _mockChannel.BasicConsumeAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>())
            .Returns("consumer-tag-1");

        await _subscriber.SubscribeAsync("test-topic", handler);

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-topic", handler));

        Assert.Equal("RabbitMQ", ex.Provider);
        Assert.Contains("Already subscribed", ex.Message);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldCallBasicCancelAsync()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        _mockChannel.BasicConsumeAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object?>>(),
            Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>())
            .Returns("consumer-tag-1");

        await _subscriber.SubscribeAsync("test-topic", handler);
        await _subscriber.UnsubscribeAsync("test-topic");

        await _mockChannel.Received(1).BasicCancelAsync("consumer-tag-1",
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldThrow_WhenTopicIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.UnsubscribeAsync(null!));
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldNotThrow_WhenNotSubscribed()
    {
        // Should be a no-op
        await _subscriber.UnsubscribeAsync("nonexistent-topic");
    }

    [Fact]
    public async Task DisposeAsync_ShouldCloseChannelAndConnection()
    {
        var subscriber = new RabbitMqSubscriber(_mockConnection, _mockChannel, _mockSerializer, _options, ownsConnection: true);
        await subscriber.DisposeAsync();

        await _mockChannel.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        _mockChannel.Received(1).Dispose();
        await _mockConnection.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        _mockConnection.Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        var subscriber = new RabbitMqSubscriber(_mockConnection, _mockChannel, _mockSerializer, _options, ownsConnection: true);
        await subscriber.DisposeAsync();
        await subscriber.DisposeAsync();

        _mockChannel.Received(1).Dispose();
        _mockConnection.Received(1).Dispose();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenConnectionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqSubscriber(null!, _mockChannel, _mockSerializer, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenChannelIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqSubscriber(_mockConnection, null!, _mockSerializer, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqSubscriber(_mockConnection, _mockChannel, null!, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqSubscriber(_mockConnection, _mockChannel, _mockSerializer, null!));
    }
}

