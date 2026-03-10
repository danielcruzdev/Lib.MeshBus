namespace Lib.MeshBus.Abstractions;

/// <summary>
/// Factory for resolving named <see cref="IMeshBusPublisher"/> instances registered with
/// <c>AddProducer(name).UseXxx(...)</c> during DI configuration.
/// </summary>
public interface IMeshBusPublisherFactory
{
    /// <summary>
    /// Returns the publisher registered under the given name.
    /// </summary>
    /// <param name="name">The unique name used when calling <c>AddProducer(name)</c>.</param>
    /// <exception cref="InvalidOperationException">No publisher registered with the given name.</exception>
    IMeshBusPublisher GetPublisher(string name);
}
