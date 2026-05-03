namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the AWS SNS provider.
/// </summary>
public class SnsOptions : MeshBusOptions
{
    /// <summary>
    /// Custom service URL, used to target LocalStack or any
    /// SNS-compatible endpoint (e.g., "http://localhost:4566").
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
    /// When true, the provider attempts to create missing SNS topics automatically.
    /// Useful for local development with LocalStack.
    /// </summary>
    public bool AutoCreateTopics { get; set; } = false;

    /// <summary>
    /// When true and subscribing, the provider creates an SQS queue and subscribes it
    /// to the SNS topic automatically for pull-based delivery.
    /// </summary>
    public bool AutoCreateSqsSubscription { get; set; } = false;

    /// <summary>
    /// Custom SQS service URL for the subscription queue.
    /// When null, uses the same region/credentials as SNS.
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
    /// Account ID used to resolve SQS queue URLs when <see cref="SqsServiceUrl"/> is set.
    /// </summary>
    public string? AccountId { get; set; }
}
