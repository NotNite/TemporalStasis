using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TemporalStasis.Encryption;
using TemporalStasis.Structs;

namespace TemporalStasis;

internal sealed class LobbyConnection(
    TcpClient clientTcp,
    IPEndPoint originalEndpoint,
    LobbyProxyConfig proxyConfig,
    IZoneProxy? zoneProxy
) : IConnection {
    public event IConnection.PacketFrameReceivedDelegate? OnPacketFrameReceived;
    public event IConnection.PacketSegmentReceivedDelegate? OnPacketSegmentReceived;
    public event IConnection.IpcPacketReceivedDelegate? OnIpcPacketReceived;

    public ConnectionType? Type { get; private set; }

    private readonly TcpClient serverTcp = new();
    private readonly NetworkStream clientStream = clientTcp.GetStream();
    private NetworkStream? serverStream;

    private readonly SemaphoreSlim clientSemaphore = new(1);
    private readonly SemaphoreSlim serverSemaphore = new(1);

    private Brokefish? brokefish;

    public async Task StartAsync(CancellationToken cancellationToken = default) {
        await this.serverTcp.ConnectAsync(originalEndpoint, cancellationToken);
        this.serverStream = this.serverTcp.GetStream();

        var clientToServer = Util.WrapTcpErrors(() => this.Proxy(
            this.clientStream, this.serverStream,
            this.serverSemaphore, DestinationType.Serverbound,
            cancellationToken
        ));
        var serverToClient = Util.WrapTcpErrors(() => this.Proxy(
            this.serverStream, this.clientStream,
            this.clientSemaphore, DestinationType.Clientbound,
            cancellationToken
        ));

        await Task.WhenAny(clientToServer, serverToClient);
    }

    private async Task Proxy(
        NetworkStream src, NetworkStream dest,
        SemaphoreSlim semaphore, DestinationType destinationType,
        CancellationToken cancellationToken = default
    ) {
        // Constant buffers that we re-use to be speedy
        Memory<byte> readBuffer = new byte[IConnection.BufferSize];
        Memory<byte> writeBuffer = new byte[IConnection.BufferSize];

        while (src.CanRead && dest.CanWrite && !cancellationToken.IsCancellationRequested) {
            {
                // Read the frame header
                var frameHeaderMemory = readBuffer[..FrameHeader.StructSize];
                await src.ReadExactlyAsync(frameHeaderMemory, cancellationToken);
                ref var frameHeader = ref MemoryMarshal.AsRef<FrameHeader>(frameHeaderMemory.Span);

                if (this.Type is null && frameHeader.ConnectionType != ConnectionType.None) {
                    this.Type = frameHeader.ConnectionType;
                }

                // `size` includes the frame header struct, don't have to subtract here
                var size = (int) frameHeader.Size;
                var frameDataMemory = readBuffer[FrameHeader.StructSize..size];
                await src.ReadExactlyAsync(frameDataMemory, cancellationToken);
            }

            // This is done twice because we can't bring the `ref` across async boundaries
            var readFrameHeaderMemory = readBuffer[..FrameHeader.StructSize];
            ref var readFrameHeader = ref MemoryMarshal.AsRef<FrameHeader>(readFrameHeaderMemory.Span);
            readFrameHeaderMemory.CopyTo(writeBuffer[..FrameHeader.StructSize]); // Copy to write buffer

            // Loop for segments
            var readPos = FrameHeader.StructSize;
            var writePos = FrameHeader.StructSize;
            var writeCount = (ushort) 0;
            for (var i = 0; i < readFrameHeader.Count; i++) {
                var segmentHeaderMemory = readBuffer.Slice(readPos, SegmentHeader.StructSize);
                // Copy to write buffer
                segmentHeaderMemory.CopyTo(writeBuffer.Slice(writePos, SegmentHeader.StructSize));

                ref var segmentHeader = ref MemoryMarshal.AsRef<SegmentHeader>(segmentHeaderMemory.Span);
                var segmentSize = (int) segmentHeader.Size;

                var segmentDataStart = readPos + SegmentHeader.StructSize;
                var segmentDataLen = segmentSize - SegmentHeader.StructSize;
                var segmentData = readBuffer.Slice(segmentDataStart, segmentDataLen);

                // Initialize Blowfish if we haven't already
                if (segmentHeader.SegmentType is SegmentType.EncryptionInit
                    && destinationType is DestinationType.Serverbound
                    && this.brokefish is null) {
                    this.brokefish = this.CreateBrokefish(segmentData.Span);
                }

                // Decrypt in place
                var isIpc = segmentHeader.SegmentType is SegmentType.Ipc;
                var isEncrypted = this.brokefish is not null && isIpc;
                if (isEncrypted) this.brokefish!.DecipherPadded(segmentData.Span);

                var segmentDropped = false;
                try {
                    var packet = new PacketSegment(ref readFrameHeader, ref segmentHeader, ref segmentData);
                    this.OnPacketSegmentReceived?.Invoke(ref packet, destinationType, ref segmentDropped);
                } catch {
                    // ignored
                }

                if (isIpc && !segmentDropped) {
                    var ipcHeaderMemory = readBuffer.Slice(segmentDataStart, IpcHeader.StructSize);
                    ref var ipcHeader = ref MemoryMarshal.AsRef<IpcHeader>(ipcHeaderMemory.Span);

                    var ipcDataStart = segmentDataStart + IpcHeader.StructSize;
                    var ipcDataLen = segmentDataLen - IpcHeader.StructSize;
                    var ipcData = readBuffer.Slice(ipcDataStart, ipcDataLen);

                    if (zoneProxy is not null && ipcHeader.Opcode == proxyConfig.EnterWorldOpcode &&
                        destinationType is DestinationType.Clientbound) {
                        try {
                            this.OverwriteEnterWorld(ipcData.Span);
                        } catch {
                            // ignored
                        }
                    }

                    try {
                        var packet = new IpcPacket(ref readFrameHeader, ref segmentHeader, ref ipcHeader, ref ipcData);
                        this.OnIpcPacketReceived?.Invoke(ref packet, destinationType, ref segmentDropped);
                    } catch {
                        // ignored
                    }
                }

                readPos += segmentSize;
                if (!segmentDropped) {
                    var segmentDataPos = writePos + SegmentHeader.StructSize;
                    segmentData.CopyTo(writeBuffer.Slice(segmentDataPos, segmentDataLen));
                    writePos += segmentSize;
                    writeCount++;
                }
            }

            // Not sending anything, doesn't matter
            if (writeCount == 0) continue;

            // Validate the write frame
            ref var writeFrameHeader =
                ref MemoryMarshal.AsRef<FrameHeader>(writeBuffer.Span[..FrameHeader.StructSize]);
            writeFrameHeader.Size = (uint) writePos;
            writeFrameHeader.DecompressedSize = writeFrameHeader.Size - (uint) FrameHeader.StructSize;
            writeFrameHeader.Count = writeCount;

            var frameDropped = false;
            try {
                var frameDataMemory = writeBuffer[FrameHeader.StructSize..writePos];
                var packet = new PacketFrame(ref writeFrameHeader, ref frameDataMemory);
                this.OnPacketFrameReceived?.Invoke(ref packet, destinationType, ref frameDropped);
            } catch {
                // ignored
            }
            if (frameDropped) continue;

            // Re-encrypt Blowfish, we do this after the event so the event frame is decrypted
            var encryptPos = FrameHeader.StructSize;
            for (var i = 0; i < writeFrameHeader.Count; i++) {
                var segmentHeaderMemory = writeBuffer.Slice(encryptPos, SegmentHeader.StructSize);
                ref var segmentHeader = ref MemoryMarshal.AsRef<SegmentHeader>(segmentHeaderMemory.Span);
                var segmentSize = (int) segmentHeader.Size;

                var segmentDataStart = encryptPos + SegmentHeader.StructSize;
                var segmentDataLen = segmentSize - SegmentHeader.StructSize;
                var segmentData = writeBuffer.Slice(segmentDataStart, segmentDataLen);

                var isIpc = segmentHeader.SegmentType is SegmentType.Ipc;
                var isEncrypted = this.brokefish is not null && isIpc;
                if (isEncrypted) this.brokefish!.EncipherPadded(segmentData.Span);

                encryptPos += segmentSize;
            }

            // Finally, send it off
            await semaphore.WaitAsync(cancellationToken);
            try {
                await dest.WriteAsync(writeBuffer[..writePos], cancellationToken);
            } finally {
                semaphore.Release();
            }
        }
    }

    private void OverwriteEnterWorld(Span<byte> data) {
        if (zoneProxy is null) return;

        var packetPort = BitConverter.ToUInt16(data[proxyConfig.EnterWorldPortOffset..]);
        var hostStart = proxyConfig.EnterWorldHostOffset;
        var hostEnd = hostStart + proxyConfig.EnterWorldHostSize;
        var hostSpan = data[hostStart..hostEnd];
        var packetHost = Encoding.UTF8.GetString(hostSpan).TrimEnd('\0');

        zoneProxy.SetNextServer(new IPEndPoint(IPAddress.Parse(packetHost), packetPort));

        BitConverter.GetBytes((ushort) zoneProxy.PublicEndpoint.Port)
            .CopyTo(data[proxyConfig.EnterWorldPortOffset..]);

        hostSpan.Clear();
        Encoding.UTF8.GetBytes(zoneProxy.PublicEndpoint.Address.ToString()).CopyTo(hostSpan);
    }

    private Brokefish CreateBrokefish(ReadOnlySpan<byte> data) {
        var key = new byte[0x2C];
        var span = key.AsSpan();

        BitConverter.GetBytes(0x12345678u).CopyTo(span); // lmfao
        data.Slice(proxyConfig.EncryptionInitKeyOffset, 4).CopyTo(span[4..]);
        BitConverter.GetBytes(proxyConfig.EncryptionKeyVersion).CopyTo(span[8..]);
        data.Slice(proxyConfig.EncryptionInitPhraseOffset, proxyConfig.EncryptionInitPhraseSize).CopyTo(span[12..]);

        var md5 = MD5.HashData(span);
        return new Brokefish(md5);
    }

    public async Task SendPacketFrameAsync(
        DestinationType destinationType,
        FrameHeader frameHeader,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        var dest = destinationType is DestinationType.Clientbound ? this.clientStream : this.serverStream;
        if (dest is null) throw new Exception("Network stream for destination not initialized");
        var semaphore = destinationType is DestinationType.Clientbound ? this.clientSemaphore : this.serverSemaphore;

        frameHeader.Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        frameHeader.Size = (uint) (FrameHeader.StructSize + data.Length);
        frameHeader.DecompressedSize = (uint) data.Length;
        frameHeader.Count = 1;
        frameHeader.CompressionType = CompressionType.None;
        if (this.Type is { } type) frameHeader.ConnectionType = type;

        Memory<byte> copied = new byte[FrameHeader.StructSize + data.Length];
        MemoryMarshal.Write(copied.Span[..FrameHeader.StructSize], frameHeader);
        data.CopyTo(copied[FrameHeader.StructSize..]);

        await semaphore.WaitAsync(cancellationToken);
        try {
            await dest.WriteAsync(copied, cancellationToken);
        } finally {
            semaphore.Release();
        }
    }

    public async Task SendPacketSegmentAsync(
        DestinationType destinationType,
        FrameHeader frameHeader,
        SegmentHeader segmentHeader,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        segmentHeader.Size = (uint) (SegmentHeader.StructSize + data.Length);

        Memory<byte> copied = new byte[SegmentHeader.StructSize + data.Length];
        MemoryMarshal.Write(copied.Span[..SegmentHeader.StructSize], segmentHeader);

        {
            var slice = copied[SegmentHeader.StructSize..];
            data.CopyTo(slice);
            if (segmentHeader.SegmentType is SegmentType.Ipc && this.brokefish is not null) {
                this.brokefish.EncipherPadded(slice.Span);
            }
        }

        await this.SendPacketFrameAsync(destinationType, frameHeader, copied, cancellationToken);
    }

    public async Task SendIpcPacketAsync(
        DestinationType destinationType,
        FrameHeader frameHeader,
        SegmentHeader segmentHeader,
        IpcHeader ipcHeader,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    ) {
        ipcHeader.Timestamp = (uint) DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Memory<byte> copied = new byte[IpcHeader.StructSize + data.Length];
        MemoryMarshal.Write(copied.Span[..IpcHeader.StructSize], ipcHeader);
        data.CopyTo(copied[IpcHeader.StructSize..]);

        await this.SendPacketSegmentAsync(destinationType, frameHeader, segmentHeader, copied, cancellationToken);
    }

    public void Dispose() {
        this.clientStream.Dispose();
        this.serverStream?.Dispose();

        clientTcp.Dispose();
        this.serverTcp.Dispose();

        this.clientSemaphore.Dispose();
        this.serverSemaphore.Dispose();
    }
}
