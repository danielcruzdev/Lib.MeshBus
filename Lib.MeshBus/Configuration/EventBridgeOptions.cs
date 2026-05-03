namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the AWS EventBridge provider.
/// </summary>
public class EventBridgeOptions : MeshBusOptions
{
    /// <summary>
    /// Custom service URL, used to target LocalStack or any
    /// EventBridge-compatible endpoint (e.g., "http://localhost:4566").
    /// Leave null to use the real AWS endpoint resolved from <see cref="RegionName"/>.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// AWS region name (e.g., "us-east-1"). Ignored when <see cref="ServiceUrl"/> is set.
    /// </summary>
    public string RegionName { get; set; } = "us-east-1";

    /// <summary>
    /// AWS access key ID. Leave null to use the default credential chain.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// AWS secret access key. Required when <see cref="AccessKey"/> is provided.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// The name of the EventBridge event bus.
    /// Defaults to "default" (the account's default event bus).
    /// </summary>
    public string EventBusName { get; set; } = "default";

    /// <summary>
    /// The event source name used when publishing events.
    /// Defaults to "meshbus".
    /// </summary>
    public string Source { get; set; } = "meshbus";

    /// <summary>
    /// When true, the subscriber auto-creates an SQS queue and EventBridge rule
    /// for pull-based consumption.
    /// </summary>
    public bool AutoCreateSqsTarget { get; set; } = false;

    /// <summary>
    /// Custom SQS service URL for the subscription queue.
    /// When null, uses the same region/credentials as EventBridge.
    /// </summary>
    public string? SqsServiceUrl { get; set; }

    /// <summary>
    /// Long-polling wait time in seconds for SQS subscription (1–20).
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Maximum number of messages to retrieve per SQS polling request (1–10).
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Account ID used by the subscriber to set up SQS queue policies.
    /// </summary>
    public string? AccountId { get; set; }
}
