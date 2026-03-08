using System.Text.Json;
using Lib.MeshBus.Serialization;

namespace Lib.MeshBus.Tests.Core;

public class SystemTextJsonSerializerTests
{
    private readonly SystemTextJsonSerializer _serializer = new();

    [Fact]
    public void Serialize_ShouldSerializeString()
    {
        var result = _serializer.Serialize("hello");

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void Deserialize_ShouldDeserializeString()
    {
        var data = _serializer.Serialize("hello");
        var result = _serializer.Deserialize<string>(data);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveComplexObject()
    {
        var original = new TestObject
        {
            Name = "Test",
            Value = 42,
            IsActive = true,
            Tags = ["tag1", "tag2"]
        };

        var data = _serializer.Serialize(original);
        var result = _serializer.Deserialize<TestObject>(data);

        Assert.NotNull(result);
        Assert.Equal(original.Name, result.Name);
        Assert.Equal(original.Value, result.Value);
        Assert.Equal(original.IsActive, result.IsActive);
        Assert.Equal(original.Tags, result.Tags);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveInteger()
    {
        var data = _serializer.Serialize(42);
        var result = _serializer.Deserialize<int>(data);

        Assert.Equal(42, result);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveDouble()
    {
        var data = _serializer.Serialize(3.14);
        var result = _serializer.Deserialize<double>(data);

        Assert.Equal(3.14, result);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveBoolean()
    {
        var data = _serializer.Serialize(true);
        var result = _serializer.Deserialize<bool>(data);

        Assert.True(result);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveNull()
    {
        var data = _serializer.Serialize<string?>(null);
        var result = _serializer.Deserialize<string?>(data);

        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_ShouldReturnDefault_WhenDataIsNull()
    {
        var result = _serializer.Deserialize<string>(null!);

        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_ShouldReturnDefault_WhenDataIsEmpty()
    {
        var result = _serializer.Deserialize<string>([]);

        Assert.Null(result);
    }

    [Fact]
    public void Constructor_ShouldAcceptCustomOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var serializer = new SystemTextJsonSerializer(options);

        var data = serializer.Serialize(new TestObject { Name = "Test", Value = 1 });
        Assert.NotNull(data);
        Assert.True(data.Length > 0);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemTextJsonSerializer(null!));
    }

    [Fact]
    public void RoundTrip_ShouldPreserveList()
    {
        var original = new List<int> { 1, 2, 3 };
        var data = _serializer.Serialize(original);
        var result = _serializer.Deserialize<List<int>>(data);

        Assert.NotNull(result);
        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveDictionary()
    {
        var original = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        var data = _serializer.Serialize(original);
        var result = _serializer.Deserialize<Dictionary<string, string>>(data);

        Assert.NotNull(result);
        Assert.Equal(original, result);
    }

    public class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public bool IsActive { get; set; }
        public List<string> Tags { get; set; } = [];
    }
}

