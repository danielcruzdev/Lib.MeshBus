using Lib.MeshBus.Exceptions;

namespace Lib.MeshBus.Tests.Core;

public class MeshBusExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        var exception = new MeshBusException("test error");

        Assert.Equal("test error", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Null(exception.Provider);
    }

    [Fact]
    public void Constructor_WithMessageAndInner_ShouldSetBoth()
    {
        var inner = new InvalidOperationException("inner error");
        var exception = new MeshBusException("test error", inner);

        Assert.Equal("test error", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(exception.Provider);
    }

    [Fact]
    public void Constructor_WithMessageInnerAndProvider_ShouldSetAll()
    {
        var inner = new InvalidOperationException("inner error");
        var exception = new MeshBusException("test error", inner, "Kafka");

        Assert.Equal("test error", exception.Message);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal("Kafka", exception.Provider);
    }

    [Fact]
    public void ShouldBeException()
    {
        var exception = new MeshBusException("test");

        Assert.IsAssignableFrom<Exception>(exception);
    }

    [Fact]
    public void Provider_ShouldSupportDifferentProviders()
    {
        var kafkaEx = new MeshBusException("err", new Exception(), "Kafka");
        var rabbitEx = new MeshBusException("err", new Exception(), "RabbitMQ");
        var azureEx = new MeshBusException("err", new Exception(), "AzureServiceBus");

        Assert.Equal("Kafka", kafkaEx.Provider);
        Assert.Equal("RabbitMQ", rabbitEx.Provider);
        Assert.Equal("AzureServiceBus", azureEx.Provider);
    }
}

