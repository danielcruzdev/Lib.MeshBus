namespace Lib.MeshBus.Configuration;

/// <summary>
/// Base configuration options for all MeshBus providers.
/// </summary>
public abstract class MeshBusOptions
{
    /// <summary>
    /// Connection string for the message broker.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Additional provider-specific properties.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
}

