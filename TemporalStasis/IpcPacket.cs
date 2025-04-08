using TemporalStasis.Structs;

namespace TemporalStasis;

/// <summary>An IPC packet, located within a <see cref="PacketSegment">packet segment</see>.</summary>
public ref struct IpcPacket {
    public ref FrameHeader FrameHeader;
    public ref SegmentHeader SegmentHeader;
    public ref IpcHeader IpcHeader;
    public Span<byte> Data;
}
