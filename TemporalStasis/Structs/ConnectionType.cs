namespace TemporalStasis.Structs;

/// <summary>The type of connection, included in <see cref="FrameHeader">frame headers</see>.</summary>
/// <remarks>This may be <see cref="ConnectionType.None"/> in some scenarios.</remarks>
public enum ConnectionType : ushort {
    None = 0,
    Zone = 1,
    Chat = 2,
    Lobby = 3
}
