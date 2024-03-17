using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketSegmentHeader {
    public uint Size;
    public uint SourceActor;
    public uint TargetActor;
    public SegmentType SegmentType;
    public ushort Unknown16;
}
