namespace Lib.MeshBus.Abstractions;

/// <summary>
/// Factory for resolving named <see cref="IMeshBusSubscriber"/> instances registered with
/// <c>AddConsumer(name).UseXxx(...)</c> during DI configuration.
/// </summary>
public interface IMeshBusSubscriberFactory
{
    /// <summary>
    /// Returns the subscriber registered under the given name.
    /// </summary>
    /// <param name="name">The unique name used when calling <c>AddConsumer(name)</c>.</param>
    /// <exception cref="InvalidOperationException">No subscriber registered with the given name.</exception>
    IMeshBusSubscriber GetSubscriber(string name);
}
