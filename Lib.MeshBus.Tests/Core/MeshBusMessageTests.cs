using Lib.MeshBus.Models;

namespace Lib.MeshBus.Tests.Core;

public class MeshBusMessageTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        var message = new MeshBusMessage<string>();

        Assert.NotNull(message.Id);
        Assert.NotEqual(string.Empty, message.Id);
        Assert.True(message.Timestamp <= DateTimeOffset.UtcNow);
        Assert.NotNull(message.Headers);
        Assert.Empty(message.Headers);
        Assert.Null(message.CorrelationId);
        Assert.Equal(string.Empty, message.Topic);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIds()
    {
        var message1 = new MeshBusMessage<string>();
        var message2 = new MeshBusMessage<string>();

        Assert.NotEqual(message1.Id, message2.Id);
    }

    [Fact]
    public void Create_ShouldSetBodyAndTopic()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");

        Assert.Equal("Hello", message.Body);
        Assert.Equal("test-topic", message.Topic);
        Assert.NotNull(message.Id);
        Assert.Null(message.CorrelationId);
    }

    [Fact]
    public void Create_ShouldSetCorrelationId_WhenProvided()
    {
        var message = MeshBusMessage<string>.Create("Hello", "test-topic", "corr-123");

        Assert.Equal("corr-123", message.CorrelationId);
    }

    [Fact]
    public void Create_ShouldSetTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var message = MeshBusMessage<string>.Create("Hello", "test-topic");
        var after = DateTimeOffset.UtcNow;

        Assert.True(message.Timestamp >= before);
        Assert.True(message.Timestamp <= after);
    }

    [Fact]
    public void Headers_ShouldBeSettable()
    {
        var message = new MeshBusMessage<string>();
        message.Headers["key"] = "value";

        Assert.Single(message.Headers);
        Assert.Equal("value", message.Headers["key"]);
    }

    [Fact]
    public void Body_ShouldSupportComplexTypes()
    {
        var body = new TestPayload { Name = "Test", Value = 42 };
        var message = MeshBusMessage<TestPayload>.Create(body, "test-topic");

        Assert.Equal("Test", message.Body.Name);
        Assert.Equal(42, message.Body.Value);
    }

    [Fact]
    public void Properties_ShouldBeModifiable()
    {
        var message = new MeshBusMessage<string>
        {
            Id = "custom-id",
            Topic = "custom-topic",
            Body = "custom-body",
            CorrelationId = "custom-corr",
            Timestamp = DateTimeOffset.MinValue
        };

        Assert.Equal("custom-id", message.Id);
        Assert.Equal("custom-topic", message.Topic);
        Assert.Equal("custom-body", message.Body);
        Assert.Equal("custom-corr", message.CorrelationId);
        Assert.Equal(DateTimeOffset.MinValue, message.Timestamp);
    }

    public class TestPayload
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}

