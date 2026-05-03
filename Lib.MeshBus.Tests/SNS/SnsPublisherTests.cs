using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Lib.MeshBus.Sns;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Lib.MeshBus.Tests.SNS;

public class SnsPublisherTests
{
    private readonly IAmazonSimpleNotificationService _mockSns;
    private readonly IMessageSerializer _mockSerializer;
    private readonly SnsTopicResolver _resolver;
    private readonly SnsPublisher _publisher;

    public SnsPublisherTests()
    {
        _mockSns = Substitute.For<IAmazonSimpleNotificationService>();
        _mockSerializer = Substitute.For<IMessageSerializer>();

        var options = new SnsOptions { AutoCreateTopics = true };

        // CreateTopicAsync returns the ARN
        _mockSns.CreateTopicAsync(Arg.Any<CreateTopicRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateTopicResponse { TopicArn = "arn:aws:sns:us-east-1:000000000000:test-topic" });

        _resolver = new SnsTopicResolver(_mockSns, options);
        _publisher = new SnsPublisher(_mockSns, _mockSerializer, _resolver);
    }

    [Fact]
    public async Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockSns.PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async Task PublishAsync_ShouldCallPublishAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockSns.PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        await _mockSns.Received(1).PublishAsync(
            Arg.Any<PublishRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowMeshBusException_OnSnsException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockSns.PublishAsync(Arg.Any<PublishRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonSimpleNotificationServiceException("Connection refused"));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("SNS", ex.Provider);
    }

    [Fact]
    public async Task PublishAsync_ShouldTargetCorrectTopicArn()
    {
        var message = MeshBusMessage<string>.Create("Hello", "my-topic");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockSns.CreateTopicAsync(Arg.Any<CreateTopicRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateTopicResponse { TopicArn = "arn:aws:sns:us-east-1:000000000000:my-topic" });

        PublishRequest? captured = null;
        _mockSns.PublishAsync(
            Arg.Do<PublishRequest>(r => captured = r),
            Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Contains("my-topic", captured.TopicArn);
    }

    [Fact]
    public async Task PublishAsync_ShouldEmbedMetadataInEnvelope()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic", correlationId: "corr-99");
        message.Headers["x-trace"] = "abc";
        _mockSerializer.Serialize("Hello").Returns([10, 20]);

        PublishRequest? captured = null;
        _mockSns.PublishAsync(
            Arg.Do<PublishRequest>(r => captured = r),
            Arg.Any<CancellationToken>())
            .Returns(new PublishResponse { MessageId = "msg-1" });

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Contains("\"CorrelationId\":\"corr-99\"", captured.Message);
        Assert.Contains("\"x-trace\":\"abc\"", captured.Message);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }
}
