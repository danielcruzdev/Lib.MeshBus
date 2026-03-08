namespace Lib.MeshBus.Configuration;

/// <summary>
/// Configuration options for the RabbitMQ provider.
/// </summary>
public class RabbitMqOptions : MeshBusOptions
{
    /// <summary>
    /// RabbitMQ server hostname (default: "localhost").
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ server port (default: 5672).
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Virtual host to use.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Exchange name for publishing messages.
    /// </summary>
    public string ExchangeName { get; set; } = "meshbus";

    /// <summary>
    /// Exchange type: "direct", "fanout", "topic", or "headers".
    /// </summary>
    public string ExchangeType { get; set; } = "topic";

    /// <summary>
    /// Whether queues and exchanges should be durable.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Whether to auto-delete queues when no consumers are connected.
    /// </summary>
    public bool AutoDelete { get; set; } = false;
}

