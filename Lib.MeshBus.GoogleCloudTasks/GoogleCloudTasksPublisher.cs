using System.Text.Json;
using Google.Cloud.Tasks.V2;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using CloudTask = Google.Cloud.Tasks.V2.Task;
using HttpMethod = Google.Cloud.Tasks.V2.HttpMethod;

namespace Lib.MeshBus.GoogleCloudTasks;

/// <summary>
/// Google Cloud Tasks implementation of <see cref="IMeshBusPublisher"/>.
/// Creates HTTP tasks in a Cloud Tasks queue. The MeshBus topic is used as the
/// queue name, and the message is sent as the HTTP request body.
/// </summary>
public class GoogleCloudTasksPublisher : IMeshBusPublisher
{
    private readonly CloudTasksClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly GoogleCloudTasksOptions _options;
    private bool _disposed;

    /// <summary>Creates a new <see cref="GoogleCloudTasksPublisher"/>.</summary>
    public GoogleCloudTasksPublisher(
        CloudTasksClient client,
        IMessageSerializer serializer,
        GoogleCloudTasksOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var queueName = QueueName.FromProjectLocationQueue(
            _options.ProjectId, _options.LocationId, message.Topic);

        if (_options.AutoCreateQueues)
            await EnsureQueueExistsAsync(queueName, cancellationToken);

        try
        {
            var task = CreateTask(message);
            await _client.CreateTaskAsync(queueName, task, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw new MeshBusException(
                $"Failed to create task in queue '{message.Topic}': {ex.Status.Detail}",
                ex,
                "GoogleCloudTasks");
        }
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var byTopic = messages.GroupBy(m => m.Topic);

        foreach (var group in byTopic)
        {
            var queueName = QueueName.FromProjectLocationQueue(
                _options.ProjectId, _options.LocationId, group.Key);

            if (_options.AutoCreateQueues)
                await EnsureQueueExistsAsync(queueName, cancellationToken);

            foreach (var message in group)
            {
                try
                {
                    var task = CreateTask(message);
                    await _client.CreateTaskAsync(queueName, task, cancellationToken);
                }
                catch (RpcException ex)
                {
                    throw new MeshBusException(
                        $"Failed to create task in queue '{group.Key}': {ex.Status.Detail}",
                        ex,
                        "GoogleCloudTasks");
                }
            }
        }
    }

    private CloudTask CreateTask<T>(MeshBusMessage<T> message)
    {
        var bodyBytes = _serializer.Serialize(message.Body);
        var envelope = new CloudTasksMessageEnvelope
        {
            Id = message.Id,
            Topic = message.Topic,
            Timestamp = message.Timestamp,
            CorrelationId = message.CorrelationId,
            Headers = message.Headers,
            Body = Convert.ToBase64String(bodyBytes)
        };

        var envelopeJson = JsonSerializer.Serialize(envelope);
        var targetUrl = $"{_options.TargetBaseUrl?.TrimEnd('/')}/{message.Topic}";

        var task = new CloudTask
        {
            HttpRequest = new HttpRequest
            {
                HttpMethod = HttpMethod.Post,
                Url = targetUrl,
                Body = ByteString.CopyFromUtf8(envelopeJson),
                Headers = { { "Content-Type", "application/json" } }
            }
        };

        return task;
    }

    private async System.Threading.Tasks.Task EnsureQueueExistsAsync(QueueName queueName, CancellationToken ct)
    {
        try
        {
            var locationName = Google.Api.Gax.ResourceNames.LocationName.FromProjectLocation(_options.ProjectId, _options.LocationId);
            await _client.CreateQueueAsync(locationName, new Queue { Name = queueName.ToString() }, ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // Queue already exists — expected.
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}
