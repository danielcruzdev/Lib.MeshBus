using System.Collections.Concurrent;
using Google.Cloud.Tasks.V2;
using Grpc.Core;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.GoogleCloudTasks;

/// <summary>
/// Google Cloud Tasks implementation of <see cref="IMeshBusSubscriber"/>.
/// Cloud Tasks is primarily a push-based service (dispatches HTTP requests to targets),
/// but this subscriber provides a pull-based consumption model by listing tasks in a queue,
/// processing them, and deleting completed tasks.
/// </summary>
public class GoogleCloudTasksSubscriber : IMeshBusSubscriber
{
    private readonly CloudTasksClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly GoogleCloudTasksOptions _options;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeSubscriptions = new();
    private readonly ConcurrentDictionary<string, System.Threading.Tasks.Task> _consumeLoops = new();
    private bool _disposed;

    /// <summary>Creates a new <see cref="GoogleCloudTasksSubscriber"/>.</summary>
    public GoogleCloudTasksSubscriber(
        CloudTasksClient client,
        IMessageSerializer serializer,
        GoogleCloudTasksOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public System.Threading.Tasks.Task SubscribeAsync<T>(
        string topic,
        Func<MeshBusMessage<T>, System.Threading.Tasks.Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_activeSubscriptions.ContainsKey(topic))
            throw new MeshBusException(
                $"Already subscribed to queue '{topic}'.",
                new InvalidOperationException(),
                "GoogleCloudTasks");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeSubscriptions[topic] = cts;

        var loopTask = System.Threading.Tasks.Task.Run(
            async () => await ConsumeLoopAsync(topic, handler, cts.Token));
        _consumeLoops[topic] = loopTask;

        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task UnsubscribeAsync(
        string topic,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_activeSubscriptions.TryRemove(topic, out var cts))
        {
            cts.Cancel();

            if (_consumeLoops.TryRemove(topic, out var loopTask))
            {
                try { await loopTask.ConfigureAwait(false); } catch { }
            }

            cts.Dispose();
        }
    }

    private async System.Threading.Tasks.Task ConsumeLoopAsync<T>(
        string topic,
        Func<MeshBusMessage<T>, System.Threading.Tasks.Task> handler,
        CancellationToken ct)
    {
        var queueName = QueueName.FromProjectLocationQueue(
            _options.ProjectId, _options.LocationId, topic);

        if (_options.AutoCreateQueues)
            await EnsureQueueExistsAsync(queueName, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tasks = _client.ListTasks(new ListTasksRequest
                {
                    Parent = queueName.ToString(),
                    ResponseView = Google.Cloud.Tasks.V2.Task.Types.View.Full,
                    PageSize = _options.MaxTasks
                });

                var processedAny = false;

                foreach (var task in tasks)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var body = task.HttpRequest?.Body?.ToStringUtf8();
                        if (string.IsNullOrEmpty(body)) continue;

                        var meshMessage = CloudTasksMessageEnvelope.ToMeshBusMessage<T>(body, _serializer);
                        await handler(meshMessage);

                        // Delete the processed task.
                        await _client.DeleteTaskAsync(task.Name, ct);
                        processedAny = true;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                    catch
                    {
                        // Handler errors: don't delete — task remains for retry.
                    }
                }

                if (!processedAny)
                {
                    await System.Threading.Tasks.Task.Delay(
                        _options.EmptyPollDelay ?? TimeSpan.FromMilliseconds(1000), ct)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (RpcException ex) when (ct.IsCancellationRequested)
            {
                _ = ex;
                break;
            }
            catch (RpcException ex)
            {
                throw new MeshBusException(
                    $"Error listing tasks from queue '{topic}': {ex.Status.Detail}",
                    ex,
                    "GoogleCloudTasks");
            }
        }
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
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var cts in _activeSubscriptions.Values)
            cts.Cancel();

        foreach (var loop in _consumeLoops.Values)
        {
            try { await loop.ConfigureAwait(false); } catch { }
        }
    }
}
