using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcHeader {
    public ushort Unknown0;
    public ushort Opcode;
    public ushort Unknown4;
    public ushort ServerId;
    public uint Timestamp;
    public uint Unknown12;
}
