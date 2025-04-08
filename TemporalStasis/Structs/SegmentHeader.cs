using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SegmentHeader {
    internal static readonly int StructSize = Unsafe.SizeOf<SegmentHeader>();

    public uint Size;
    public uint SourceActor;
    public uint TargetActor;
    public SegmentType SegmentType;
    public ushort Unknown16;
}
