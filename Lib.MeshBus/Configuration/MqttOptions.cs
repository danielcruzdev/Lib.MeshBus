namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the MQTT provider (compatible with any MQTT 5.0 broker, e.g., HiveMQ).
/// </summary>
public class MqttOptions : MeshBusOptions
{
    /// <summary>
    /// The MQTT broker host (e.g., "localhost" or "broker.hivemq.com").
    /// </summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// The MQTT broker port. Default is 1883 (unencrypted) or 8883 (TLS).
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// The MQTT client identifier. A unique ID is generated if not specified.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Whether to use TLS/SSL for the connection.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Optional username for broker authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional password for broker authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The MQTT Quality of Service level: "AtMostOnce" (0), "AtLeastOnce" (1), or "ExactlyOnce" (2).
    /// Defaults to "AtLeastOnce".
    /// </summary>
    public string QualityOfService { get; set; } = "AtLeastOnce";

    /// <summary>
    /// Whether to start a clean MQTT session (discard previous session state on connect).
    /// </summary>
    public bool CleanStart { get; set; } = true;
}
