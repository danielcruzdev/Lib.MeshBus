namespace Lib.MeshBus.Models;

/// <summary>
/// Represents a message envelope that wraps the payload with metadata.
/// </summary>
/// <typeparam name="T">The type of the message body.</typeparam>
public class MeshBusMessage<T>
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Custom headers/metadata for the message.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// The message payload.
    /// </summary>
    public T Body { get; set; } = default!;

    /// <summary>
    /// Optional correlation identifier for tracing related messages.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The topic/queue name this message is associated with.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new MeshBusMessage with auto-generated Id and Timestamp.
    /// </summary>
    /// <param name="body">The message payload.</param>
    /// <param name="topic">The target topic/queue name.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <returns>A new MeshBusMessage instance.</returns>
    public static MeshBusMessage<T> Create(T body, string topic, string? correlationId = null)
    {
        return new MeshBusMessage<T>
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Body = body,
            Topic = topic,
            CorrelationId = correlationId
        };
    }
}

