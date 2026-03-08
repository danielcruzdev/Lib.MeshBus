using System.Text.Json;
using Lib.MeshBus.Abstractions;

namespace Lib.MeshBus.Serialization;

/// <summary>
/// Default message serializer using System.Text.Json.
/// </summary>
public class SystemTextJsonSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates a new instance with default JsonSerializerOptions.
    /// </summary>
    public SystemTextJsonSerializer()
        : this(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        })
    {
    }

    /// <summary>
    /// Creates a new instance with the specified JsonSerializerOptions.
    /// </summary>
    /// <param name="options">Custom JSON serializer options.</param>
    public SystemTextJsonSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(byte[] data)
    {
        if (data is null || data.Length == 0)
            return default;

        return JsonSerializer.Deserialize<T>(data, _options);
    }
}

