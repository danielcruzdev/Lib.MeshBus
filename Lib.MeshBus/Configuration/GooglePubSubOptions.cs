namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Google Cloud Pub/Sub provider.
/// </summary>
public class GooglePubSubOptions : MeshBusOptions
{
    /// <summary>
    /// Google Cloud project ID (e.g., "my-gcp-project").
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Hostname of the Pub/Sub emulator (e.g., "localhost:8085").
    /// When set the provider connects to the emulator instead of the real Pub/Sub service.
    /// </summary>
    public string? EmulatorHost { get; set; }

    /// <summary>
    /// Suffix appended to the topic name to derive the subscription name.
    /// The full subscription name becomes "{topic}-{SubscriptionSuffix}".
    /// Defaults to "meshbus-sub".
    /// </summary>
    public string SubscriptionSuffix { get; set; } = "meshbus-sub";

    /// <summary>
    /// Acknowledgement deadline in seconds.
    /// </summary>
    public int AckDeadlineSeconds { get; set; } = 60;

    /// <summary>
    /// When true, the provider attempts to create missing topics and subscriptions
    /// automatically. Useful for local development with the Pub/Sub emulator.
    /// </summary>
    public bool AutoCreateResources { get; set; } = false;

    /// <summary>
    /// Maximum number of messages to pull per polling request.
    /// </summary>
    public int MaxMessages { get; set; } = 10;

    /// <summary>
    /// Delay to wait between polling requests when no messages are received.
    /// When null, defaults to 500 ms.
    /// </summary>
    public TimeSpan? EmptyPollDelay { get; set; }
}
