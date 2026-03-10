using Lib.MeshBus.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.DependencyInjection;

internal sealed class MeshBusSubscriberFactory : IMeshBusSubscriberFactory
{
    private readonly IServiceProvider _serviceProvider;

    public MeshBusSubscriberFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IMeshBusSubscriber GetSubscriber(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _serviceProvider.GetRequiredKeyedService<IMeshBusSubscriber>(name);
    }
}
