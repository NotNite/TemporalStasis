using TemporalStasis.Structs;

namespace TemporalStasis;

/// <summary>A generic connection created by an <see cref="IProxy"/>.</summary>
/// <remarks>
/// <para>
/// An <see cref="IConnection"/> handles two TCP connections, one from the client and one to the server, for a total of
/// four streams. <seealso cref="DestinationType"/>
/// </para>
///
/// <para>
/// When connecting to a <see cref="IZoneProxy">zone server</see>, the game client connects twice (for separate
/// <see cref="ConnectionType.Zone">zone</see> and <see cref="ConnectionType.Chat">chat</see> connections). This means
/// the same player will have two separate <see cref="IConnection"/> instances.
/// </para>
/// </remarks>
public interface IConnection : IDisposable {
    /// <summary>
    /// Size for the internal buffer allocated to read packets into. It is assumed packet frames will never exceed this.
    /// </summary>
    internal const int BufferSize = 0x4000;

    /// <summary>Called when a packet frame is received from the client or server.</summary>
    /// <seealso cref="PacketFrame"/>
    /// <remarks>
    /// This event is invoked last, after <see cref="IConnection.OnPacketSegmentReceived"/> and
    /// <see cref="IConnection.OnIpcPacketReceived"/>.
    /// </remarks>
    /// <param name="packetFrame">The packet frame.</param>
    /// <param name="destinationType">The destination for this packet, indicating where it was sent from.</param>
    /// <param name="dropped">Set to <c>true</c> to drop this packet, preventing it from being sent.</param>
    public delegate void PacketFrameReceivedDelegate(
        ref PacketFrame packetFrame, DestinationType destinationType, ref bool dropped
    );

    /// <summary>Called when a packet segment is received from the client or server.</summary>
    /// <seealso cref="PacketSegment"/>
    /// <remarks>
    /// This event is invoked first, before <see cref="IConnection.OnIpcPacketReceived"/> and
    /// <see cref="IConnection.OnPacketFrameReceived"/>.
    /// </remarks>
    /// <param name="packetSegment">The packet segment.</param>
    /// <param name="destinationType">The destination for this packet, indicating where it was sent from.</param>
    /// <param name="dropped">Set to <c>true</c> to drop this packet, preventing it from being sent.</param>
    public delegate void PacketSegmentReceivedDelegate(
        ref PacketSegment packetSegment, DestinationType destinationType, ref bool dropped
    );

    /// <summary>Called when an IPC packet is received from the client or server.</summary>
    /// <seealso cref="IpcPacket"/>
    /// <remarks>
    /// This event is invoked second, after <see cref="IConnection.OnPacketSegmentReceived"/> and before
    /// <see cref="IConnection.OnPacketFrameReceived"/>.
    /// </remarks>
    /// <param name="ipcPacket">The IPC packet.</param>
    /// <param name="destinationType">The destination for this packet, indicating where it was sent from.</param>
    /// <param name="dropped">Set to <c>true</c> to drop this packet, preventing it from being sent.</param>
    public delegate void IpcPacketReceivedDelegate(
        ref IpcPacket ipcPacket, DestinationType destinationType, ref bool dropped
    );

    /// <inheritdoc cref="PacketFrameReceivedDelegate"/>
    public event PacketFrameReceivedDelegate? OnPacketFrameReceived;

    /// <inheritdoc cref="PacketSegmentReceivedDelegate"/>
    public event PacketSegmentReceivedDelegate? OnPacketSegmentReceived;

    /// <inheritdoc cref="IpcPacketReceivedDelegate"/>
    public event IpcPacketReceivedDelegate? OnIpcPacketReceived;

    /// <summary>The established connection type for this connection.</summary>
    public ConnectionType? Type { get; }

    /// <summary>Send ("replay") a packet frame to the given destination.</summary>
    /// <remarks>This method assumes the provided data is not compressed, if applicable.</remarks>
    public Task SendPacketFrameAsync(
        DestinationType destinationType, FrameHeader frameHeader, ReadOnlyMemory<byte> data
    );

    /// <summary>Send ("replay") a packet segment to the given destination.</summary>
    /// <remarks>This method assumes the provided data is not encrypted, if applicable.</remarks>
    public Task SendPacketSegmentAsync(
        DestinationType destinationType, SegmentHeader segmentHeader, ReadOnlyMemory<byte> data
    );

    /// <summary>Send ("replay") an IPC packet to the given destination.</summary>
    /// <remarks>This method assumes the provided data is obfuscated, if applicable.</remarks>
    public Task SendIpcPacketAsync(
        DestinationType destinationType, uint sourceActor, uint targetActor,
        IpcHeader ipcHeader, ReadOnlyMemory<byte> data
    );

    /// <summary>Start and run the connection, continuing until it is cancelled or the client disconnects.</summary>
    internal Task StartAsync(CancellationToken cancellationToken = default);
}
