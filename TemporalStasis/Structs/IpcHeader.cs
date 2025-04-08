using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TemporalStasis.Structs;

/// <summary>The header for an <see cref="IpcPacket">IPC packet</see>.</summary>
/// <remarks>This struct exactly correlates with the network and memory layout.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcHeader {
    internal static readonly int StructSize = Unsafe.SizeOf<IpcHeader>();

    public ushort Unknown0;

    /// <summary>The opcode of this IPC packet.</summary>
    /// <remarks>
    /// Opcodes of IPC packets are randomized every game patch. Temporal Stasis does not maintain a list of IPC packet
    /// opcodes, and you are expected to know the relevant opcodes if you are listening for specific packets.
    /// </remarks>
    public ushort Opcode;
    public ushort Unknown4;
    public ushort ServerId;

    /// <summary>A UNIX timestamp in seconds.</summary>
    public uint Timestamp;
    public uint Unknown12;
}
