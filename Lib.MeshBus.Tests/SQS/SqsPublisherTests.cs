using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Sqs;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.SQS;

public class SqsPublisherTests
{
    private readonly IAmazonSQS _mockSqs;
    private readonly IMessageSerializer _mockSerializer;
    private readonly SqsQueueResolver _resolver;
    private readonly SqsPublisher _publisher;

    public SqsPublisherTests()
    {
        _mockSqs = Substitute.For<IAmazonSQS>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        var options = new SqsOptions
        {
            ServiceUrl = "http://localhost:9324",
            AccountId = "000000000000"
        };

        _resolver = new SqsQueueResolver(_mockSqs, options);
        _publisher = new SqsPublisher(_mockSqs, _mockSerializer, _resolver);
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockSqs.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldCallSendMessageAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockSqs.SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        await _mockSqs.Received(1).SendMessageAsync(
            Arg.Any<SendMessageRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldTargetCorrectQueueUrl()
    {
        var message = MeshBusMessage<string>.Create("Hello", "my-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        SendMessageRequest? captured = null;
        _mockSqs.SendMessageAsync(
            Arg.Do<SendMessageRequest>(r => captured = r),
            Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.EndsWith("/my-queue", captured.QueueUrl);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnSqsException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockSqs
            .SendMessageAsync(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonSQSException("Connection refused"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("SQS", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldEmbedMetadataInEnvelope()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue", correlationId: "corr-99");
        message.Headers["x-trace"] = "abc";
        _mockSerializer.Serialize("Hello").Returns([10, 20]);

        SendMessageRequest? captured = null;
        _mockSqs.SendMessageAsync(
            Arg.Do<SendMessageRequest>(r => captured = r),
            Arg.Any<CancellationToken>())
            .Returns(new SendMessageResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Contains("\"CorrelationId\":\"corr-99\"", captured.MessageBody);
        Assert.Contains("\"x-trace\":\"abc\"", captured.MessageBody);
        Assert.Contains("\"Id\":\"" + message.Id + "\"", captured.MessageBody);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }
}
