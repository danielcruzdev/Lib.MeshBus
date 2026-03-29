using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Sqs;
using NSubstitute;

namespace Lib.MeshBus.Tests.SQS;

public class SqsSubscriberTests
{
    private readonly IAmazonSQS _mockSqs;
    private readonly IMessageSerializer _mockSerializer;
    private readonly SqsOptions _options;
    private readonly SqsQueueResolver _resolver;
    private readonly SqsSubscriber _subscriber;

    public SqsSubscriberTests()
    {
        _mockSqs = Substitute.For<IAmazonSQS>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new SqsOptions
        {
            ServiceUrl = "http://localhost:9324",
            AccountId = "000000000000",
            WaitTimeSeconds = 0,
            MaxNumberOfMessages = 1
        };
        _resolver = new SqsQueueResolver(_mockSqs, _options);
        _subscriber = new SqsSubscriber(_mockSqs, _mockSerializer, _resolver, _options);
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
    public async Task SubscribeAsync_ShouldThrow_WhenAlreadySubscribed()
    {
        // Immediately return empty response to avoid a tight loop
        _mockSqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReceiveMessageResponse { Messages = [] });

        var handler = Substitute.For<Func<MeshBusMessage<string>, Task>>();
        await _subscriber.SubscribeAsync("test-queue", handler);

        var ex = Assert.Throws<MeshBusException>(() =>
            _subscriber.SubscribeAsync("test-queue", handler).GetAwaiter().GetResult());

        Assert.Equal("SQS", ex.Provider);
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
        await _subscriber.UnsubscribeAsync("not-subscribed");
    }

    [Fact]
    public async Task SubscribeAsync_ShouldReceiveAndDeserializeMessages()
    {
        var received = new List<MeshBusMessage<string>>();

        // Set up a valid SQS message body (MeshBus envelope format)
        var envelope = new SqsMessageEnvelope
        {
            Id = "test-id",
            Topic = "test-queue",
            Timestamp = DateTimeOffset.UtcNow,
            Headers = [],
            Body = Convert.ToBase64String([1, 2, 3])
        };
        var json = System.Text.Json.JsonSerializer.Serialize(envelope);

        var callCount = 0;
        _mockSqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (callCount++ == 0)
                    return Task.FromResult(new ReceiveMessageResponse
                    {
                        Messages =
                        [
                            new Message
                            {
                                MessageId = "sqs-1",
                                ReceiptHandle = "rh-1",
                                Body = json
                            }
                        ]
                    });

                return Task.FromResult(new ReceiveMessageResponse { Messages = [] });
            });

        _mockSqs.DeleteMessageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteMessageResponse());

        _mockSerializer.Deserialize<string>(Arg.Any<byte[]>()).Returns("Hello");

        await _subscriber.SubscribeAsync<string>("test-queue", msg =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        });

        await Task.Delay(300);

        Assert.Single(received);
        Assert.Equal("test-id", received[0].Id);
        Assert.Equal("test-queue", received[0].Topic);

        await _mockSqs.Received(1).DeleteMessageAsync(
            Arg.Any<string>(), "rh-1", Arg.Any<CancellationToken>());
    }

    // Expose envelope for test construction
    private sealed class SqsMessageEnvelope
    {
        public string Id { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string? CorrelationId { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];
        public string Body { get; set; } = string.Empty;
    }
}
