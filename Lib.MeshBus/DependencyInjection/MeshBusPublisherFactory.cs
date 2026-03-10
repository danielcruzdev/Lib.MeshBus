using Lib.MeshBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.DependencyInjection;

internal sealed class MeshBusPublisherFactory : IMeshBusPublisherFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MeshBusPublisherFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMeshBusPublisher GetPublisher(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _serviceProvider.GetRequiredKeyedService<IMeshBusPublisher>(name);
    }
}
