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
}

