namespace TemporalStasis.Structs;

/// <summary>An IPC packet, located within a <see cref="PacketSegment">packet segment</see>.</summary>
public ref struct IpcPacket {
    public ref FrameHeader FrameHeader;
    public ref SegmentHeader SegmentHeader;
    public ref IpcHeader IpcHeader;
    public Memory<byte> Data;
}
