using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.DependencyInjection;

/// <summary>
/// Builder returned by <see cref="MeshBusBuilder.AddProducer"/> that allows a specific
/// messaging provider to be configured for a named publisher.
/// </summary>
public class NamedProducerBuilder
{
    /// <summary>The unique name for this producer registration.</summary>
    public string Name { get; }

    /// <summary>The underlying service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Creates a new <see cref="NamedProducerBuilder"/>.
    /// </summary>
    public NamedProducerBuilder(string name, IServiceCollection services)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}
