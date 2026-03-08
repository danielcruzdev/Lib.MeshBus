using Azure.Messaging.ServiceBus;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.AzureServiceBus;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.AzureServiceBus;

public class AzureServiceBusSubscriberTests
{
    private readonly ServiceBusClient _mockClient;
    private readonly ServiceBusProcessor _mockProcessor;
    private readonly IMessageSerializer _mockSerializer;
    private readonly AzureServiceBusOptions _options;
    private readonly AzureServiceBusSubscriber _subscriber;

    public AzureServiceBusSubscriberTests()
    {
        _mockClient = Substitute.For<ServiceBusClient>();
        _mockProcessor = Substitute.For<ServiceBusProcessor>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new AzureServiceBusOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = true
        };

        _mockClient.CreateProcessor(Arg.Any<string>(), Arg.Any<ServiceBusProcessorOptions>())
            .Returns(_mockProcessor);
        _mockClient.CreateProcessor(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ServiceBusProcessorOptions>())
            .Returns(_mockProcessor);

        _subscriber = new AzureServiceBusSubscriber(_mockClient, _mockSerializer, _options);
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCreateProcessor_InQueueMode()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test-queue", handler);

        _mockClient.Received(1).CreateProcessor("test-queue", Arg.Any<ServiceBusProcessorOptions>());
    }

    [Fact]
    public async Task SubscribeAsync_ShouldCreateProcessor_InTopicMode()
    {
        var optionsWithSub = new AzureServiceBusOptions
        {
            ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test",
            SubscriptionName = "my-subscription"
        };
        var subscriber = new AzureServiceBusSubscriber(_mockClient, _mockSerializer, optionsWithSub);
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await subscriber.SubscribeAsync("test-topic", handler);

        _mockClient.Received(1).CreateProcessor("test-topic", "my-subscription", Arg.Any<ServiceBusProcessorOptions>());
    }

    [Fact]
    public async Task SubscribeAsync_ShouldStartProcessing()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();

        await _subscriber.SubscribeAsync("test-queue", handler);

        await _mockProcessor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
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
            _subscriber.SubscribeAsync<string>("test-queue", null!));
    }

    [Fact]
    public async Task SubscribeAsync_ShouldThrowMeshBusException_WhenAlreadySubscribed()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-queue", handler);

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-queue", handler));

        Assert.Equal("AzureServiceBus", ex.Provider);
        Assert.Contains("Already subscribed", ex.Message);
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldStopProcessing()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-queue", handler);

        await _subscriber.UnsubscribeAsync("test-queue");

        await _mockProcessor.Received(1).StopProcessingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnsubscribeAsync_ShouldDisposeProcessor()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-queue", handler);

        await _subscriber.UnsubscribeAsync("test-queue");

        await _mockProcessor.Received(1).DisposeAsync();
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
        await _subscriber.UnsubscribeAsync("nonexistent-topic");
        // Should be a no-op
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAllProcessorsAndClient()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-queue", handler);

        await _subscriber.DisposeAsync();

        await _mockProcessor.Received().StopProcessingAsync(Arg.Any<CancellationToken>());
        await _mockClient.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldOnlyDisposeOnce()
    {
        await _subscriber.DisposeAsync();
        await _subscriber.DisposeAsync();

        await _mockClient.Received(1).DisposeAsync();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureServiceBusSubscriber(null!, _mockSerializer, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSerializerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureServiceBusSubscriber(_mockClient, null!, _options));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AzureServiceBusSubscriber(_mockClient, _mockSerializer, null!));
    }
}

