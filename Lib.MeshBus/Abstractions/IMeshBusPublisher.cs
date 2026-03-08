using Lib.MeshBus.Models;

namespace Lib.MeshBus.Abstractions;

/// <summary>
/// Defines the contract for publishing messages to a message broker.
/// </summary>
public interface IMeshBusPublisher : IAsyncDisposable
{
    /// <summary>
    /// Publishes a single message to the specified topic.
    /// </summary>
    /// <typeparam name="T">The type of the message body.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<T>(MeshBusMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a batch of messages to the specified topic.
    /// </summary>
    /// <typeparam name="T">The type of the message body.</typeparam>
    /// <param name="messages">The messages to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishBatchAsync<T>(IEnumerable<MeshBusMessage<T>> messages, CancellationToken cancellationToken = default);
}

