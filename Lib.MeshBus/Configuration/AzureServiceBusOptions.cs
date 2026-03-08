namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Azure Service Bus provider.
/// </summary>
public class AzureServiceBusOptions : MeshBusOptions
{
    /// <summary>
    /// Fully qualified namespace (e.g., "mynamespace.servicebus.windows.net").
    /// Either this or ConnectionString must be provided.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Subscription name for topic subscriptions.
    /// Required when subscribing to topics.
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Maximum number of concurrent calls to the message handler.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Whether to automatically complete messages after the handler returns.
    /// </summary>
    public bool AutoCompleteMessages { get; set; } = true;

    /// <summary>
    /// Maximum time to wait for a message before timing out.
    /// </summary>
    public TimeSpan? MaxAutoLockRenewalDuration { get; set; }
}

