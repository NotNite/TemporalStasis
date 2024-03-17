using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketHeader {
    public ulong Unknown0;
    public ulong Unknown8;
    public ulong Timestamp;
    public uint Size;
    public ConnectionType ConnectionType;
    public ushort Count;
    public byte Unknown20;
    public CompressionType CompressionType;
    public ushort Unknown24;
    public uint UncompressedSize;
}
