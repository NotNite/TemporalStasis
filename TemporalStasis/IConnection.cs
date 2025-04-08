using TemporalStasis.Structs;

namespace TemporalStasis;

/// <summary>A generic connection for an <see cref="IProxy"/>.</summary>
public interface IConnection : IDisposable {
    /// <summary>Size for the internal buffer allocated to read packets into.</summary>
    internal const int BufferSize = 0x10000;

    /// <summary>Called when a packet frame is received from the client or server.</summary>
    /// <remarks>Packet frames are still encrypted with Blowfish.</remarks>
    public delegate void PacketFrameReceivedDelegate(
        ref PacketFrame packetFrame, DestinationType destinationType, ref bool dropped
    );

    /// <summary>Called when a packet segment is received from the client or server.</summary>
    public delegate void PacketSegmentReceivedDelegate(
        ref PacketSegment packetSegment, DestinationType destinationType, ref bool dropped
    );

    /// <summary>Called when an IPC packet is received from the client or server.</summary>
    public delegate void IpcPacketReceivedDelegate(
        ref IpcPacket ipcPacket, DestinationType destinationType, ref bool dropped
    );

    /// <inheritdoc cref="PacketFrameReceivedDelegate"/>
    public event PacketFrameReceivedDelegate? OnPacketFrameReceived;

    /// <inheritdoc cref="PacketSegmentReceivedDelegate"/>
    public event PacketSegmentReceivedDelegate? OnPacketSegmentReceived;

    /// <inheritdoc cref="IpcPacketReceivedDelegate"/>
    public event IpcPacketReceivedDelegate? OnIpcPacketReceived;

    /// <summary>Send ("replay") a packet frame to the given destination.</summary>
    /// <remarks>This method assumes the data is not compressed or encrypted.</remarks>
    public Task SendPacketFrameAsync(
        DestinationType destinationType, FrameHeader frameHeader, ReadOnlyMemory<byte> data
    );

    /// <summary>Send ("replay") a packet segment to the given destination.</summary>
    /// <remarks>This method assumes the data is not compressed or encrypted.</remarks>
    public Task SendPacketSegmentAsync(
        DestinationType destinationType, SegmentHeader segmentHeader, ReadOnlyMemory<byte> data
    );

    /// <summary>Send ("replay") an IPC packet to the given destination.</summary>
    /// <remarks>This method assumes the data is not compressed or encrypted.</remarks>
    public Task SendIpcPacketAsync(
        DestinationType destinationType, uint sourceActor, uint targetActor,
        IpcHeader ipcHeader, ReadOnlyMemory<byte> data
    );

    /// <summary>Start and run the connection, continuing until it is cancelled or the client disconnects.</summary>
    internal Task StartAsync(CancellationToken cancellationToken = default);
}
