namespace Lib.MeshBus.Abstractions;

/// <summary>
/// Defines the contract for message serialization and deserialization.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Serializes an object to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A byte array representation of the object.</returns>
    byte[] Serialize<T>(T obj);

    /// <summary>
    /// Deserializes a byte array to an object.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>The deserialized object, or default if deserialization fails.</returns>
    T? Deserialize<T>(byte[] data);
}

