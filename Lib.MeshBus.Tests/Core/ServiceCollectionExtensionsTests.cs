using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Tests.Core;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMeshBus_ShouldReturnMeshBusBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddMeshBus();

        Assert.NotNull(builder);
        Assert.IsType<MeshBusBuilder>(builder);
    }

    [Fact]
    public void AddMeshBus_ShouldRegisterDefaultSerializer()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var serializer = provider.GetService<IMessageSerializer>();

        Assert.NotNull(serializer);
        Assert.IsType<SystemTextJsonSerializer>(serializer);
    }

    [Fact]
    public void AddMeshBus_ShouldNotOverrideExistingSerializer()
    {
        var services = new ServiceCollection();
        var customSerializer = NSubstitute.Substitute.For<IMessageSerializer>();
        services.AddSingleton(customSerializer);

        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var serializer = provider.GetService<IMessageSerializer>();

        Assert.Same(customSerializer, serializer);
    }

    [Fact]
    public void AddMeshBus_ShouldInvokeConfigureAction()
    {
        var services = new ServiceCollection();
        var configured = false;

        services.AddMeshBus(builder =>
        {
            configured = true;
            Assert.NotNull(builder);
            Assert.NotNull(builder.Services);
        });

        Assert.True(configured);
    }

    [Fact]
    public void AddMeshBus_ShouldExposeServiceCollection()
    {
        var services = new ServiceCollection();

        var builder = services.AddMeshBus();

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void AddMeshBus_CalledMultipleTimes_ShouldNotDuplicateSerializer()
    {
        var services = new ServiceCollection();

        services.AddMeshBus();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var serializers = provider.GetServices<IMessageSerializer>().ToList();

        Assert.Single(serializers);
    }
}

