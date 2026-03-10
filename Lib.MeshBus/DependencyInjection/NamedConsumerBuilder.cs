using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.DependencyInjection;

/// <summary>
/// Builder returned by <see cref="MeshBusBuilder.AddConsumer"/> that allows a specific
/// messaging provider to be configured for a named subscriber.
/// </summary>
public class NamedConsumerBuilder
{
    /// <summary>The unique name for this consumer registration.</summary>
    public string Name { get; }

    /// <summary>The underlying service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Creates a new <see cref="NamedConsumerBuilder"/>.
    /// </summary>
    public NamedConsumerBuilder(string name, IServiceCollection services)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }
}
