using Lib.MeshBus.Abstractions;
using Lib.MeshBus.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lib.MeshBus.DependencyInjection;

/// <summary>
/// Extension methods for configuring MeshBus in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MeshBus core services to the service collection and returns a builder
    /// for configuring the messaging provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional action to configure the MeshBus builder.</param>
    /// <returns>The MeshBusBuilder for chaining provider configuration.</returns>
    public static MeshBusBuilder AddMeshBus(this IServiceCollection services, Action<MeshBusBuilder>? configure = null)
    {
        // Register default serializer if not already registered
        services.TryAddSingleton<IMessageSerializer, SystemTextJsonSerializer>();

        var builder = new MeshBusBuilder(services);
        configure?.Invoke(builder);

        return builder;
    }
}

