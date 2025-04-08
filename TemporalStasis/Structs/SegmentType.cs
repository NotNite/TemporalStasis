namespace TemporalStasis.Structs;

/// <summary>The type of <see cref="PacketSegment">packet segment</see>.</summary>
/// <remarks>There are more segment types than this, but these are the only segment types Temporal Stasis uses.</remarks>
public enum SegmentType : ushort {
    Ipc = 3,
    EncryptionInit = 9
}
