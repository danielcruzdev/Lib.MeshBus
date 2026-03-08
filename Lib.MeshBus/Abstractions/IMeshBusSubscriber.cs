using Lib.MeshBus.Models;

namespace Lib.MeshBus.Abstractions;

/// <summary>
/// Defines the contract for subscribing to messages from a message broker.
/// </summary>
public interface IMeshBusSubscriber : IAsyncDisposable
{
    /// <summary>
    /// Subscribes to a topic and processes messages with the provided handler.
    /// </summary>
    /// <typeparam name="T">The type of the message body.</typeparam>
    /// <param name="topic">The topic/queue name to subscribe to.</param>
    /// <param name="handler">The async handler to process each received message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SubscribeAsync<T>(string topic, Func<MeshBusMessage<T>, Task> handler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a topic and stops processing messages.
    /// </summary>
    /// <param name="topic">The topic/queue name to unsubscribe from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);
}

