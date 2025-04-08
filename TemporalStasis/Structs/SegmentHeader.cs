using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

/// <summary>The header for a <see cref="PacketSegment">packet segment</see>.</summary>
/// <remarks>This struct exactly correlates with the network and memory layout.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SegmentHeader {
    internal static readonly int StructSize = Unsafe.SizeOf<SegmentHeader>();

    /// <summary>The size of the packet segment.</summary>
    /// <remarks>
    /// <para>This size includes the size of the segment header.</para>
    /// <para>Temporal Stasis automatically updates this for you.</para>
    /// </remarks>
    public uint Size;

    /// <summary>The source actor ID.</summary>
    public uint SourceActor;

    /// <summary>The target actor ID.</summary>
    public uint TargetActor;

    /// <summary>The segment type.</summary>
    /// <remarks>
    /// Packet segments with a <see cref="SegmentType"/> of <see cref="SegmentType.Ipc"/> contain an IPC packet in their
    /// <see cref="PacketSegment.Data">segment data</see>.
    /// </remarks>
    public SegmentType SegmentType;
    public ushort Unknown16;
}
