namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Azure Event Hubs provider.
/// </summary>
public class EventHubsOptions : MeshBusOptions
{
    /// <summary>
    /// Fully qualified Event Hubs namespace (e.g., "mynamespace.servicebus.windows.net").
    /// Used when authenticating via managed identity instead of a connection string.
    /// </summary>
    public string? FullyQualifiedNamespace { get; set; }

    /// <summary>
    /// Consumer group name.  Defaults to "$Default".
    /// </summary>
    public string ConsumerGroup { get; set; } = "$Default";

    /// <summary>
    /// Maximum number of events to read per receive call.
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
}
