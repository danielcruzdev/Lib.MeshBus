namespace Lib.MeshBus.Exceptions;

/// <summary>
/// Unified exception type for all MeshBus operations.
/// Wraps provider-specific exceptions to provide a consistent error handling experience.
/// </summary>
public class MeshBusException : Exception
{
    /// <summary>
    /// The name of the provider that threw the original exception (e.g., "Kafka", "RabbitMQ", "AzureServiceBus").
    /// </summary>
    public string? Provider { get; }

    /// <summary>
    /// Creates a new MeshBusException with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MeshBusException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new MeshBusException with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The original exception from the provider SDK.</param>
    public MeshBusException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new MeshBusException with a message, inner exception, and provider name.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The original exception from the provider SDK.</param>
    /// <param name="provider">The name of the messaging provider.</param>
    public MeshBusException(string message, Exception innerException, string provider)
        : base(message, innerException)
    {
        Provider = provider;
    }
}

