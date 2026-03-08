using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Lib.MeshBus.Kafka;
using Lib.MeshBus.Kafka.DependencyInjection;
using Lib.MeshBus.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Lib.MeshBus.Tests.Integration;

public class MeshBusBuilderTests
{
    [Fact]
    public void MeshBusBuilder_ShouldHoldServiceCollection()
    {
        var services = new ServiceCollection();
        var builder = new MeshBusBuilder(services);

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void MeshBusBuilder_ShouldThrow_WhenServicesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MeshBusBuilder(null!));
    }

    [Fact]
    public void AddMeshBus_WithUseKafka_ShouldRegisterKafkaPublisher()
    {
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseKafka(opts =>
        {
            opts.BootstrapServers = "localhost:9092";
            opts.GroupId = "test-group";
        }));

        var descriptors = services.Where(d => d.ServiceType == typeof(IMeshBusPublisher)).ToList();
        Assert.Single(descriptors);
    }

    [Fact]
    public void AddMeshBus_WithUseKafka_ShouldRegisterKafkaSubscriber()
    {
        var services = new ServiceCollection();
        services.AddMeshBus(bus => bus.UseKafka(opts =>
        {
            opts.BootstrapServers = "localhost:9092";
            opts.GroupId = "test-group";
        }));

        var descriptors = services.Where(d => d.ServiceType == typeof(IMeshBusSubscriber)).ToList();
        Assert.Single(descriptors);
    }

    [Fact]
    public void AddMeshBus_ShouldRegisterSerializer()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var serializer = provider.GetService<IMessageSerializer>();

        Assert.NotNull(serializer);
        Assert.IsType<SystemTextJsonSerializer>(serializer);
    }

    [Fact]
    public void UseKafka_ShouldThrow_WhenConfigureIsNull()
    {
        var services = new ServiceCollection();
        var builder = services.AddMeshBus();

        Assert.Throws<ArgumentNullException>(() =>
            builder.UseKafka(null!));
    }

    [Fact]
    public void AddMeshBus_FluentAPI_ShouldWork()
    {
        var services = new ServiceCollection();

        // This should compile and work
        services.AddMeshBus(bus => bus.UseKafka(opts =>
        {
            opts.BootstrapServers = "localhost:9092";
        }));

        // Verify the fluent API registered both interfaces
        Assert.Contains(services, d => d.ServiceType == typeof(IMeshBusPublisher));
        Assert.Contains(services, d => d.ServiceType == typeof(IMeshBusSubscriber));
        Assert.Contains(services, d => d.ServiceType == typeof(IMessageSerializer));
    }
}

