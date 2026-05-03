namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Apache Pulsar provider.
/// </summary>
public class PulsarOptions : MeshBusOptions
{
    /// <summary>
    /// The Pulsar service URL (e.g., "pulsar://localhost:6650").
    /// </summary>
    public string ServiceUrl { get; set; } = "pulsar://localhost:6650";

    /// <summary>
    /// The default topic to publish/subscribe to.
    /// Used only in single-provider mode; in named mode the topic is taken from the message at publish time.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// The subscription name used by the consumer.
    /// </summary>
    public string SubscriptionName { get; set; } = "meshbus-subscription";

    /// <summary>
    /// The subscription type: "Exclusive", "Shared", "Failover", or "KeyShared".
    /// Defaults to "Shared" for multi-consumer scenarios.
    /// </summary>
    public string SubscriptionType { get; set; } = "Shared";

    /// <summary>
    /// The initial position for the subscription: "Earliest" or "Latest".
    /// </summary>
    public string InitialPosition { get; set; } = "Earliest";
}
