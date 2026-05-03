using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Configuration;
using Lib.MeshBus.Exceptions;
using Lib.MeshBus.Models;
using Microsoft.Extensions.Options;

namespace Lib.MeshBus.Pulsar;

/// <summary>
/// Apache Pulsar implementation of <see cref="IMeshBusPublisher"/>.
/// </summary>
public class PulsarPublisher : IMeshBusPublisher
{
    private readonly IPulsarClient _client;
    private readonly IMessageSerializer _serializer;
    private readonly ConcurrentDictionary<string, IProducer<ReadOnlySequence<byte>>> _producers = new();
    private readonly bool _ownsClient;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="PulsarPublisher"/> using the provided Pulsar client and serializer.
    /// </summary>
    public PulsarPublisher(IPulsarClient client, IMessageSerializer serializer)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _ownsClient = false;
    }

    /// <summary>
    /// Creates a new <see cref="PulsarPublisher"/> using <see cref="PulsarOptions"/>.
    /// </summary>
    public PulsarPublisher(IOptions<PulsarOptions> options, IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(options?.Value);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        _client = PulsarClient.Builder()
            .ServiceUrl(new Uri(options.Value.ServiceUrl))
            .Build();

        _ownsClient = true;
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var producer = GetOrCreateProducer(message.Topic);
            var metadata = BuildMetadata(message);
            var bytes = _serializer.Serialize(message.Body);
            await producer.Send(metadata, new ReadOnlySequence<byte>(bytes), cancellationToken);
        }
        catch (MeshBusException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MeshBusException(
                $"Failed to publish message to topic '{message.Topic}': {ex.Message}",
                ex,
                "Pulsar");
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var exceptions = new List<Exception>();

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(message, cancellationToken);
            }
            catch (MeshBusException ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new MeshBusException(
                $"Failed to publish {exceptions.Count} message(s) in batch.",
                new AggregateException(exceptions),
                "Pulsar");
        }
    }

    private IProducer<ReadOnlySequence<byte>> GetOrCreateProducer(string topic)
    {
        if (_producers.TryGetValue(topic, out var existing))
            return existing;

        var options = new ProducerOptions<ReadOnlySequence<byte>>(topic, Schema.ByteSequence);
        var producer = _client.CreateProducer(options);
        _producers.TryAdd(topic, producer);
        return producer;
    }

    private static MessageMetadata BuildMetadata<T>(MeshBusMessage<T> message)
    {
        var metadata = new MessageMetadata();

        metadata["meshbus-message-id"] = message.Id;
        metadata["meshbus-timestamp"] = message.Timestamp.ToString("O");

        if (!string.IsNullOrEmpty(message.CorrelationId))
            metadata["meshbus-correlation-id"] = message.CorrelationId;

        foreach (var header in message.Headers)
            metadata[header.Key] = header.Value;

        return metadata;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var producer in _producers.Values)
            await producer.DisposeAsync();

        _producers.Clear();

        if (_ownsClient)
            await _client.DisposeAsync();
    }
}
