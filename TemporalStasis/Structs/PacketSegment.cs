namespace TemporalStasis.Structs;

/// <summary>A packet segment, located within a <see cref="PacketFrame">packet frame</see>.</summary>
public ref struct PacketSegment {
    public ref FrameHeader FrameHeader;
    public ref SegmentHeader SegmentHeader;
    public Memory<byte> Data;
}
