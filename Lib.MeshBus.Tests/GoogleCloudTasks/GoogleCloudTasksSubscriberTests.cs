using Google.Cloud.Tasks.V2;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.GoogleCloudTasks;
using Lib.MeshBus.Models;
using NSubstitute;

namespace Lib.MeshBus.Tests.GoogleCloudTasks;

public class GoogleCloudTasksSubscriberTests
{
    private readonly CloudTasksClient _mockClient;
    private readonly IMessageSerializer _mockSerializer;
    private readonly GoogleCloudTasksOptions _options;
    private readonly GoogleCloudTasksSubscriber _subscriber;

    public GoogleCloudTasksSubscriberTests()
    {
        _mockClient = Substitute.For<CloudTasksClient>();
        _mockSerializer = Substitute.For<IMessageSerializer>();
        _options = new GoogleCloudTasksOptions
        {
            ProjectId = "test-project",
            LocationId = "us-central1",
            TargetBaseUrl = "https://my-service.run.app"
        };
        _subscriber = new GoogleCloudTasksSubscriber(_mockClient, _mockSerializer, _options);
    }

    [Fact]
    public async System.Threading.Tasks.Task SubscribeAsync_ShouldThrow_WhenTopicIsNull()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, System.Threading.Tasks.Task>>();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.SubscribeAsync(null!, handler));
    }

    [Fact]
    public async System.Threading.Tasks.Task SubscribeAsync_ShouldThrow_WhenTopicIsEmpty()
    {
        var handler = Substitute.For<Func<MeshBusMessage<string>, System.Threading.Tasks.Task>>();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _subscriber.SubscribeAsync("", handler));
    }

    [Fact]
    public async System.Threading.Tasks.Task SubscribeAsync_ShouldThrow_WhenHandlerIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.SubscribeAsync<string>("test-queue", null!));
    }

    [Fact]
    public async System.Threading.Tasks.Task UnsubscribeAsync_ShouldThrow_WhenTopicIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _subscriber.UnsubscribeAsync(null!));
    }

    [Fact]
    public async System.Threading.Tasks.Task UnsubscribeAsync_ShouldThrow_WhenTopicIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _subscriber.UnsubscribeAsync(""));
    }

    [Fact]
    public async System.Threading.Tasks.Task UnsubscribeAsync_ShouldBeNoOp_WhenNotSubscribed()
    {
        await _subscriber.UnsubscribeAsync("not-subscribed");
    }
}
