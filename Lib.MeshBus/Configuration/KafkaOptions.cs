namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Apache Kafka provider.
/// </summary>
public class KafkaOptions : MeshBusOptions
{
    /// <summary>
    /// Comma-separated list of Kafka broker addresses (e.g., "localhost:9092").
    /// </summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Consumer group identifier.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Auto offset reset policy: "earliest", "latest", or "error".
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";

    /// <summary>
    /// Producer acknowledgment level: "all", "leader", or "none".
    /// </summary>
    public string Acks { get; set; } = "all";

    /// <summary>
    /// Whether the consumer should auto-commit offsets.
    /// </summary>
    public bool EnableAutoCommit { get; set; } = true;

    /// <summary>
    /// Enable auto-creation of topics on the broker.
    /// </summary>
    public bool AllowAutoCreateTopics { get; set; } = true;
}

