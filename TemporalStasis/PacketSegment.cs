using TemporalStasis.Structs;

namespace TemporalStasis;

/// <summary>A packet segment, located within a <see cref="PacketFrame">packet frame</see>.</summary>
/// <remarks>
/// Packet segments with a <see cref="SegmentHeader.SegmentType"/> of <see cref="SegmentType.Ipc"/> contain an IPC
/// packet in their data.
/// </remarks>
public ref struct PacketSegment {
    /// <summary>The frame header from the <see cref="PacketFrame">frame</see> this segment is in.</summary>
    public ref FrameHeader FrameHeader;
    /// <summary>The header for this segment.</summary>
    public ref SegmentHeader SegmentHeader;

    /// <summary>The segment data.</summary>
    /// <remarks>
    /// <para>
    /// When the segment's <see cref="SegmentHeader.SegmentType">segment type</see> is <see cref="SegmentType.Ipc"/>,
    /// this data contains a <see cref="IpcHeader"/> and the IPC packet data.
    /// </para>
    /// <para>
    /// When listening from <see cref="ILobbyProxy">a lobby proxy</see>, and the segment contains an IPC packet, the
    /// segment data is encrypted with Blowfish. Temporal Stasis decrypts the segment data for you.
    /// </para>
    /// </remarks>
    public Span<byte> Data => this.memory.Span;

    private readonly ref Memory<byte> memory;

    public PacketSegment(
        ref FrameHeader frameHeader,
        ref SegmentHeader segmentHeader,
        ref Memory<byte> data
    ) {
        this.FrameHeader = ref frameHeader;
        this.SegmentHeader = ref segmentHeader;
        this.memory = ref data;
    }
}
