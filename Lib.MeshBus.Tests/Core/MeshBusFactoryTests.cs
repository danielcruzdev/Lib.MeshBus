using Lib.MeshBus.Abstractions;
using Lib.MeshBus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Lib.MeshBus.Tests.Core;

public class MeshBusFactoryTests
{
    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public void AddMeshBus_ShouldRegisterPublisherFactory()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IMeshBusPublisherFactory>();

        Assert.NotNull(factory);
    }

    [Fact]
    public void AddMeshBus_ShouldRegisterSubscriberFactory()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IMeshBusSubscriberFactory>();

        Assert.NotNull(factory);
    }

    // ── MeshBusBuilder.AddProducer / AddConsumer ──────────────────────────────

    [Fact]
    public void AddProducer_ShouldReturnNamedProducerBuilderWithCorrectName()
    {
        var services = new ServiceCollection();
        var meshBusBuilder = new MeshBusBuilder(services);

        var namedBuilder = meshBusBuilder.AddProducer("my-producer");

        Assert.NotNull(namedBuilder);
        Assert.Equal("my-producer", namedBuilder.Name);
        Assert.Same(services, namedBuilder.Services);
    }

    [Fact]
    public void AddConsumer_ShouldReturnNamedConsumerBuilderWithCorrectName()
    {
        var services = new ServiceCollection();
        var meshBusBuilder = new MeshBusBuilder(services);

        var namedBuilder = meshBusBuilder.AddConsumer("my-consumer");

        Assert.NotNull(namedBuilder);
        Assert.Equal("my-consumer", namedBuilder.Name);
        Assert.Same(services, namedBuilder.Services);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddProducer_ShouldThrow_WhenNameIsNullOrWhiteSpace(string? name)
    {
        var services = new ServiceCollection();
        var meshBusBuilder = new MeshBusBuilder(services);

        Assert.ThrowsAny<ArgumentException>(() => meshBusBuilder.AddProducer(name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddConsumer_ShouldThrow_WhenNameIsNullOrWhiteSpace(string? name)
    {
        var services = new ServiceCollection();
        var meshBusBuilder = new MeshBusBuilder(services);

        Assert.ThrowsAny<ArgumentException>(() => meshBusBuilder.AddConsumer(name!));
    }

    // ── IMeshBusPublisherFactory resolution ───────────────────────────────────

    [Fact]
    public void PublisherFactory_GetPublisher_ShouldResolveCorrectInstance()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var expectedPublisher = Substitute.For<IMeshBusPublisher>();
        services.AddKeyedSingleton<IMeshBusPublisher>("kafka-orders", expectedPublisher);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusPublisherFactory>();

        var resolved = factory.GetPublisher("kafka-orders");

        Assert.Same(expectedPublisher, resolved);
    }

    [Fact]
    public void PublisherFactory_GetPublisher_ShouldResolveMultipleIndependentInstances()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var kafkaPublisher = Substitute.For<IMeshBusPublisher>();
        var rabbitPublisher = Substitute.For<IMeshBusPublisher>();
        services.AddKeyedSingleton<IMeshBusPublisher>("kafka-orders", kafkaPublisher);
        services.AddKeyedSingleton<IMeshBusPublisher>("rabbit-events", rabbitPublisher);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusPublisherFactory>();

        var resolvedKafka = factory.GetPublisher("kafka-orders");
        var resolvedRabbit = factory.GetPublisher("rabbit-events");

        Assert.Same(kafkaPublisher, resolvedKafka);
        Assert.Same(rabbitPublisher, resolvedRabbit);
        Assert.NotSame(resolvedKafka, resolvedRabbit);
    }

    [Fact]
    public void PublisherFactory_GetPublisher_ShouldThrow_WhenNameNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusPublisherFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.GetPublisher("does-not-exist"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PublisherFactory_GetPublisher_ShouldThrow_WhenNameIsNullOrWhiteSpace(string? name)
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusPublisherFactory>();

        Assert.ThrowsAny<ArgumentException>(() => factory.GetPublisher(name!));
    }

    // ── IMeshBusSubscriberFactory resolution ──────────────────────────────────

    [Fact]
    public void SubscriberFactory_GetSubscriber_ShouldResolveCorrectInstance()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var expectedSubscriber = Substitute.For<IMeshBusSubscriber>();
        services.AddKeyedSingleton<IMeshBusSubscriber>("kafka-payments", expectedSubscriber);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusSubscriberFactory>();

        var resolved = factory.GetSubscriber("kafka-payments");

        Assert.Same(expectedSubscriber, resolved);
    }

    [Fact]
    public void SubscriberFactory_GetSubscriber_ShouldResolveMultipleIndependentInstances()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var rabbitSubscriber = Substitute.For<IMeshBusSubscriber>();
        var asbSubscriber = Substitute.For<IMeshBusSubscriber>();
        services.AddKeyedSingleton<IMeshBusSubscriber>("rabbit-orders", rabbitSubscriber);
        services.AddKeyedSingleton<IMeshBusSubscriber>("asb-notifications", asbSubscriber);

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusSubscriberFactory>();

        var resolvedRabbit = factory.GetSubscriber("rabbit-orders");
        var resolvedAsb = factory.GetSubscriber("asb-notifications");

        Assert.Same(rabbitSubscriber, resolvedRabbit);
        Assert.Same(asbSubscriber, resolvedAsb);
        Assert.NotSame(resolvedRabbit, resolvedAsb);
    }

    [Fact]
    public void SubscriberFactory_GetSubscriber_ShouldThrow_WhenNameNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusSubscriberFactory>();

        Assert.Throws<InvalidOperationException>(() => factory.GetSubscriber("does-not-exist"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SubscriberFactory_GetSubscriber_ShouldThrow_WhenNameIsNullOrWhiteSpace(string? name)
    {
        var services = new ServiceCollection();
        services.AddMeshBus();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusSubscriberFactory>();

        Assert.ThrowsAny<ArgumentException>(() => factory.GetSubscriber(name!));
    }

    // ── Fluent AddMeshBus config integration ─────────────────────────────────

    [Fact]
    public void AddMeshBus_WithConfigureAction_ShouldRegisterNamedPublisherViaBuilder()
    {
        var expectedPublisher = Substitute.For<IMeshBusPublisher>();
        var services = new ServiceCollection();

        services.AddMeshBus(bus =>
        {
            // Simulate what a provider extension does
            bus.Services.AddKeyedSingleton<IMeshBusPublisher>("producer-kafka", expectedPublisher);
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeshBusPublisherFactory>();
        var resolved = factory.GetPublisher("producer-kafka");

        Assert.Same(expectedPublisher, resolved);
    }
}
