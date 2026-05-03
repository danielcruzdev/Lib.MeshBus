namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Azure Event Grid provider.
/// </summary>
public class EventGridOptions : MeshBusOptions
{
    /// <summary>
    /// The Event Grid topic endpoint (e.g., "https://my-topic.eastus-1.eventgrid.azure.net/api/events").
    /// Required for publishing events.
    /// </summary>
    public string? TopicEndpoint { get; set; }

    /// <summary>
    /// Access key for authenticating with the Event Grid topic.
    /// Either this or a connection string must be provided.
    /// </summary>
    public string? AccessKey { get; set; }

    /// <summary>
    /// The Event Grid namespace endpoint for pull delivery
    /// (e.g., "https://my-namespace.eastus-1.eventgrid.azure.net").
    /// Required for subscribing via pull delivery.
    /// </summary>
    public string? NamespaceEndpoint { get; set; }

    /// <summary>
    /// Namespace access key for pull delivery authentication.
    /// </summary>
    public string? NamespaceAccessKey { get; set; }

    /// <summary>
    /// The topic name used within Event Grid namespace for pull delivery subscriptions.
    /// </summary>
    public string? NamespaceTopicName { get; set; }

    /// <summary>
    /// The subscription name for pull delivery.
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Maximum number of events to receive per pull request.
    /// </summary>
    public int MaxEvents { get; set; } = 10;

    /// <summary>
    /// Maximum wait time in seconds for pull requests when no events are available.
    /// </summary>
    public int MaxWaitTimeSeconds { get; set; } = 10;

    /// <summary>
    /// Delay between polling requests when no events are received.
    /// When null, defaults to 500 ms.
    /// </summary>
    public TimeSpan? EmptyPollDelay { get; set; }
}
