using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.DependencyInjection;

/// <summary>
/// Builder for configuring MeshBus services and selecting a messaging provider.
/// </summary>
public class MeshBusBuilder
{
    /// <summary>
    /// The service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Creates a new MeshBusBuilder.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    public MeshBusBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Registers a named producer. Call a provider extension (e.g. <c>.UseKafka()</c>)
    /// on the returned builder to complete the registration.
    /// </summary>
    /// <param name="name">A unique name that identifies this producer.</param>
    public NamedProducerBuilder AddProducer(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NamedProducerBuilder(name, Services);
    }

    /// <summary>
    /// Registers a named consumer. Call a provider extension (e.g. <c>.UseKafka()</c>)
    /// on the returned builder to complete the registration.
    /// </summary>
    /// <param name="name">A unique name that identifies this consumer.</param>
    public NamedConsumerBuilder AddConsumer(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NamedConsumerBuilder(name, Services);
    }
}

