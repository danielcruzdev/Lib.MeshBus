using Google.Cloud.Tasks.V2;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.GoogleCloudTasks;
using Lib.MeshBus.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using CloudTask = Google.Cloud.Tasks.V2.Task;

namespace Lib.MeshBus.Tests.GoogleCloudTasks;

public class GoogleCloudTasksPublisherTests
{
    private readonly CloudTasksClient _mockClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly GoogleCloudTasksOptions _options;
    private readonly GoogleCloudTasksPublisher _publisher;

    public GoogleCloudTasksPublisherTests()
    {
        _mockClient = Substitute.For<CloudTasksClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new GoogleCloudTasksOptions
        {
            ProjectId = "test-project",
            LocationId = "us-central1",
            TargetBaseUrl = "https://my-service.run.app"
        };
        _publisher = new GoogleCloudTasksPublisher(_mockClient, _mockSerializer, _options);
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishAsync_ShouldSerializeBody()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockClient.CreateTaskAsync(
            Arg.Any<QueueName>(),
            Arg.Any<CloudTask>(),
            Arg.Any<CancellationToken>())
            .Returns(new CloudTask());

        await _publisher.PublishAsync(message);

        _mockSerializer.Received(1).Serialize("Hello");
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishAsync_ShouldCallCreateTaskAsync()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);
        _mockClient.CreateTaskAsync(
            Arg.Any<QueueName>(),
            Arg.Any<CloudTask>(),
            Arg.Any<CancellationToken>())
            .Returns(new CloudTask());

        await _publisher.PublishAsync(message);

        await _mockClient.Received(1).CreateTaskAsync(
            Arg.Any<QueueName>(),
            Arg.Any<CloudTask>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishAsync_ShouldThrow_WhenMessageIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishAsync<string>(null!));
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishAsync_ShouldThrowMeshBusException_OnRpcException()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-queue");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        _mockClient.CreateTaskAsync(
            Arg.Any<QueueName>(),
            Arg.Any<CloudTask>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "Service unavailable")));

        var ex = await Assert.ThrowsAsync<MeshBusException>(() =>
            _publisher.PublishAsync(message));

        Assert.Equal("GoogleCloudTasks", ex.Provider);
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishAsync_ShouldSetCorrectHttpTarget()
    {
        var message = MeshBusMessage<string>.Create("Hello", "orders");
        _mockSerializer.Serialize("Hello").Returns([1, 2, 3]);

        CloudTask? captured = null;
        _mockClient.CreateTaskAsync(
            Arg.Any<QueueName>(),
            Arg.Do<CloudTask>(t => captured = t),
            Arg.Any<CancellationToken>())
            .Returns(new CloudTask());

        await _publisher.PublishAsync(message);

        Assert.NotNull(captured);
        Assert.Equal("https://my-service.run.app/orders", captured.HttpRequest.Url);
        Assert.Equal(Google.Cloud.Tasks.V2.HttpMethod.Post, captured.HttpRequest.HttpMethod);
    }

    [Fact]
    public async System.Threading.Tasks.Task PublishBatchAsync_ShouldThrow_WhenMessagesIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _publisher.PublishBatchAsync<string>(null!));
    }
}
