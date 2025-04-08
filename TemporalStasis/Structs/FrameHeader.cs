using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

/// <summary>The header for a <see cref="PacketFrame">packet frame</see>.</summary>
/// <remarks>This struct exactly correlates with the network and memory layout.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FrameHeader {
    internal static readonly int StructSize = Unsafe.SizeOf<FrameHeader>();

    /// <summary>An unknown prefix, presumably magic data.</summary>
    public unsafe fixed byte Prefix[16];

    /// <summary>A UNIX timestamp in milliseconds.</summary>
    /// <remarks>May be zero in some cases.</remarks>
    public ulong Timestamp;

    /// <summary>The size of the packet frame.</summary>
    /// <remarks>
    /// <para>This size includes the size of the frame header.</para>
    /// <para>Temporal Stasis automatically updates this for you.</para>
    /// </remarks>
    public uint Size;

    /// <summary>The connection type.</summary>
    /// <remarks>This may be <see cref="ConnectionType.None"/> in some scenarios.</remarks>
    public ConnectionType ConnectionType;

    /// <summary>The amount of <see cref="PacketSegment">packet segments</see> in this frame.</summary>
    /// <remarks>Temporal Stasis automatically updates this for you.</remarks>
    public ushort Count;
    public byte Version;

    /// <summary>The compression type.</summary>
    /// <remarks>
    /// When listening from <see cref="IZoneProxy">a zone proxy</see>, and this is <see cref="CompressionType.Oodle"/>,
    /// the frame data is compressed with Oodle. Temporal Stasis decompresses the frame data for you.
    /// </remarks>
    public CompressionType CompressionType;
    public ushort Unknown24;

    /// <summary>The size of the frame data after being decompressed.</summary>
    /// <remarks>
    /// <para>Temporal Stasis automatically updates this for you.</para>
    /// <para>
    /// This size does not include the size of the frame header. When the frame is not compressed, this is equal to
    /// <see cref="Size"/> minus the size of the frame header.
    /// </para>
    /// </remarks>
    public uint DecompressedSize;
}
