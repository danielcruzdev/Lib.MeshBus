namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the AWS SQS provider.
/// </summary>
public class SqsOptions : MeshBusOptions
{
    /// <summary>
    /// Custom service URL, used to target LocalStack, ElasticMQ, or any
    /// SQS-compatible endpoint (e.g., "http://localhost:9324").
    /// Leave null to use the real AWS endpoint resolved from <see cref="RegionName"/>.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// AWS region name (e.g., "us-east-1"). Ignored when <see cref="ServiceUrl"/> is set.
    /// </summary>
    public string RegionName { get; set; } = "us-east-1";

    /// <summary>
    /// AWS access key ID. Leave null to use the default credential chain (environment
    /// variables, ~/.aws/credentials, instance profile, etc.).
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// AWS secret access key. Required when <see cref="AccessKey"/> is provided.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>
    /// Long-polling wait time in seconds (1–20). Higher values reduce API calls.
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Maximum number of messages to retrieve per polling request (1–10).
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// When true, the provider attempts to create missing SQS queues automatically.
    /// Useful for local development with LocalStack / ElasticMQ.
    /// </summary>
    public bool AutoCreateQueues { get; set; } = false;

    /// <summary>
    /// Account ID used to resolve queue URLs when <see cref="ServiceUrl"/> is set
    /// (e.g., "000000000000" for LocalStack). Leave null for real AWS.
    /// </summary>
    public string? AccountId { get; set; }
}
