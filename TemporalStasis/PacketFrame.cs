using TemporalStasis.Structs;

namespace TemporalStasis;

/// <summary>A packet frame. Packet frames are the main container of FFXIV packets.</summary>
/// <remarks>Packet frames contain one or more <see cref="PacketSegment">packet segments</see> in them.</remarks>
public ref struct PacketFrame {
    /// <summary>The header for this frame.</summary>
    public ref FrameHeader FrameHeader;

    /// <summary>The frame data.</summary>
    /// <remarks>
    /// When listening from <see cref="IZoneProxy">a zone proxy</see>, and the frame's
    /// <see cref="FrameHeader.CompressionType">compression type</see> is <see cref="CompressionType.Oodle"/>,
    /// the frame data is compressed with Oodle. Temporal Stasis decompresses the frame data for you.
    /// </remarks>
    public Span<byte> Data => this.memory.Span;

    private readonly ref Memory<byte> memory;

    public PacketFrame(
        ref FrameHeader frameHeader,
        ref Memory<byte> data
    ) {
        this.FrameHeader = ref frameHeader;
        this.memory = ref data;
    }
}
