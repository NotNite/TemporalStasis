namespace TemporalStasis.Structs;

/// <summary>The compression type used for a <see cref="PacketFrame">packet frame</see>.</summary>
/// <remarks><see cref="Zlib"/> is not actively used by the retail game, and Temporal Stasis does not support it.</remarks>
public enum CompressionType : byte {
    None = 0,
    Zlib = 1,
    Oodle = 2
}
