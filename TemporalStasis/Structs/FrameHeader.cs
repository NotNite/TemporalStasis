using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FrameHeader {
    internal static readonly int StructSize = Unsafe.SizeOf<FrameHeader>();

    public unsafe fixed byte Prefix[16];
    public ulong Timestamp;
    public uint Size;
    public ConnectionType ConnectionType;
    public ushort Count;
    public byte Version;
    public CompressionType CompressionType;
    public ushort Unknown24;
    public uint DecompressedSize;
}
