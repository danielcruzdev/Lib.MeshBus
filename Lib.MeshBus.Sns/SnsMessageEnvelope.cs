using System.Text.Json;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Models;

namespace Lib.MeshBus.Sns;

/// <summary>
/// JSON envelope used as the SNS message body.
/// Wraps all MeshBus metadata and the serialized payload.
/// </summary>
internal sealed class SnsMessageEnvelope
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>Base64-encoded bytes produced by <see cref="IMessageSerializer"/>.</summary>
    public string Body { get; set; } = string.Empty;

    internal static MeshBusMessage<T> ToMeshBusMessage<T>(string rawBody, IMessageSerializer serializer)
    {
        var envelope = JsonSerializer.Deserialize<SnsMessageEnvelope>(rawBody)
            ?? throw new InvalidOperationException("Failed to deserialize SNS message envelope.");

        var bodyBytes = Convert.FromBase64String(envelope.Body);
        var body = serializer.Deserialize<T>(bodyBytes);

        var message = new MeshBusMessage<T>
        {
            Id = envelope.Id,
            Topic = envelope.Topic,
            Timestamp = envelope.Timestamp,
            CorrelationId = envelope.CorrelationId,
            Body = body!
        };

        foreach (var kvp in envelope.Headers)
            message.Headers[kvp.Key] = kvp.Value;

        return message;
    }
}
