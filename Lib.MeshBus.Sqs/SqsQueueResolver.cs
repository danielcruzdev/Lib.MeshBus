using System.Collections.Concurrent;
using Amazon.SQS;
using Amazon.SQS.Model;
using Lib.MeshBus.Configuration;

namespace Lib.MeshBus.Sqs;

/// <summary>
/// Resolves SQS queue URLs from queue names, optionally creating queues that do not yet exist.
/// Results are cached per queue name.
/// </summary>
public sealed class SqsQueueResolver
{
    private readonly IAmazonSQS _sqsClient;
    private readonly SqsOptions _options;
    private readonly ConcurrentDictionary<string, string> _urlCache = new();

    /// <summary>Creates a new <see cref="SqsQueueResolver"/>.</summary>
    public SqsQueueResolver(IAmazonSQS sqsClient, SqsOptions options)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Returns the queue URL for the given queue name, creating the queue when
    /// <see cref="SqsOptions.AutoCreateQueues"/> is true and the queue does not exist.
    /// </summary>
    public async Task<string> GetOrCreateQueueUrlAsync(string queueName, CancellationToken ct = default)
    {
        if (_urlCache.TryGetValue(queueName, out var cached))
            return cached;

        string url;

        // When a custom ServiceUrl is configured we can derive the URL directly
        // and skip the GetQueueUrl API call (useful for ElasticMQ / LocalStack).
        if (!string.IsNullOrEmpty(_options.ServiceUrl) && !string.IsNullOrEmpty(_options.AccountId))
        {
            url = $"{_options.ServiceUrl.TrimEnd('/')}/{_options.AccountId}/{queueName}";
            if (_options.AutoCreateQueues)
                await EnsureQueueExistsAsync(queueName, ct);
        }
        else
        {
            try
            {
                var response = await _sqsClient.GetQueueUrlAsync(queueName, ct);
                url = response.QueueUrl;
            }
            catch (QueueDoesNotExistException) when (_options.AutoCreateQueues)
            {
                var createResponse = await _sqsClient.CreateQueueAsync(
                    new CreateQueueRequest { QueueName = queueName }, ct);
                url = createResponse.QueueUrl;
            }
        }

        _urlCache[queueName] = url;
        return url;
    }

    private async Task EnsureQueueExistsAsync(string queueName, CancellationToken ct)
    {
        try
        {
            await _sqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = queueName }, ct);
        }
        catch (QueueNameExistsException)
        {
            // Queue already exists — that is fine.
        }
    }
}
