using System.Collections.Concurrent;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.Sns;

/// <summary>
/// Resolves SNS topic ARNs from topic names, optionally creating topics that do not yet exist.
/// Results are cached per topic name.
/// </summary>
public sealed class SnsTopicResolver
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly SnsOptions _options;
    private readonly ConcurrentDictionary<string, string> _arnCache = new();

    /// <summary>Creates a new <see cref="SnsTopicResolver"/>.</summary>
    public SnsTopicResolver(IAmazonSimpleNotificationService snsClient, SnsOptions options)
    {
        _snsClient = snsClient ?? throw new ArgumentNullException(nameof(snsClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Returns the topic ARN for the given topic name, creating the topic when
    /// <see cref="SnsOptions.AutoCreateTopics"/> is true and the topic does not exist.
    /// </summary>
    public async Task<string> GetOrCreateTopicArnAsync(string topicName, CancellationToken ct = default)
    {
        if (_arnCache.TryGetValue(topicName, out var cached))
            return cached;

        try
        {
            var response = await _snsClient.CreateTopicAsync(new CreateTopicRequest
            {
                Name = topicName
            }, ct);

            var arn = response.TopicArn;
            _arnCache[topicName] = arn;
            return arn;
        }
        catch (AmazonSimpleNotificationServiceException ex) when (!_options.AutoCreateTopics)
        {
            throw new MeshBusException(
                $"Failed to resolve topic ARN for '{topicName}': {ex.Message}",
                ex,
                "SNS");
        }
    }
}
