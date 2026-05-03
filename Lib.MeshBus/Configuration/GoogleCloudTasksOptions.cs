namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the Google Cloud Tasks provider.
/// </summary>
public class GoogleCloudTasksOptions : MeshBusOptions
{
    /// <summary>
    /// Google Cloud project ID (e.g., "my-gcp-project").
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The Google Cloud region/location (e.g., "us-central1").
    /// </summary>
    public string LocationId { get; set; } = string.Empty;

    /// <summary>
    /// The base URL for the HTTP target where tasks will be dispatched
    /// (e.g., "https://my-service.run.app").
    /// Required when publishing tasks.
    /// </summary>
    public string? TargetBaseUrl { get; set; }

    /// <summary>
    /// Hostname of the Cloud Tasks emulator (e.g., "localhost:8123").
    /// When set, the provider connects to the emulator instead of the real Cloud Tasks service.
    /// </summary>
    public string? EmulatorHost { get; set; }

    /// <summary>
    /// When true, the provider attempts to create missing queues automatically.
    /// Useful for local development with an emulator.
    /// </summary>
    public bool AutoCreateQueues { get; set; } = false;

    /// <summary>
    /// Delay between polling requests when no tasks are available.
    /// When null, defaults to 1000 ms.
    /// </summary>
    public TimeSpan? EmptyPollDelay { get; set; }

    /// <summary>
    /// Maximum number of tasks to list per polling request.
    /// </summary>
    public int MaxTasks { get; set; } = 10;
}
