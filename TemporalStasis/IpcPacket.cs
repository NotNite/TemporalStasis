using TemporalStasis.Structs;

namespace TemporalStasis;

/// <summary>An IPC packet, located within a <see cref="PacketSegment">packet segment</see>.</summary>
public ref struct IpcPacket {
    /// <summary>The frame header from the <see cref="PacketFrame">frame</see> this packet is in.</summary>
    public ref FrameHeader FrameHeader;
    /// <summary>The segment header from the <see cref="PacketSegment">segment</see> this packet is in.</summary>
    public ref SegmentHeader SegmentHeader;
    /// <summary>The header for this packet.</summary>
    public ref IpcHeader IpcHeader;

    /// <summary>The packet data.</summary>
    /// <remarks>
    /// Since Patch 7.2, some packets from the zone server have obfuscated fields. Temporal Stasis does not deobfuscate
    /// these packet fields, and you are expected to not modify those fields unless you know what you are doing.
    /// </remarks>
    public Span<byte> Data => this.memory.Span;

    private readonly ref Memory<byte> memory;

    public IpcPacket(
        ref FrameHeader frameHeader,
        ref SegmentHeader segmentHeader,
        ref IpcHeader ipcHeader,
        ref Memory<byte> data
    ) {
        this.FrameHeader = ref frameHeader;
        this.SegmentHeader = ref segmentHeader;
        this.IpcHeader = ref ipcHeader;
        this.memory = ref data;
    }
}
