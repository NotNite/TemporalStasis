using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TemporalStasis.Encryption;
using TemporalStasis.Structs;

namespace TemporalStasis;

internal sealed class LobbyConnection(
    TcpClient clientTcp,
    IPEndPoint originalEndpoint,
    LobbyProxyConfig proxyConfig
) : IConnection {
    public event IConnection.PacketFrameReceivedDelegate? OnPacketFrameReceived;
    public event IConnection.PacketSegmentReceivedDelegate? OnPacketSegmentReceived;
    public event IConnection.IpcPacketReceivedDelegate? OnIpcPacketReceived;

    private readonly TcpClient serverTcp = new();

    private readonly NetworkStream clientStream = clientTcp.GetStream();
    private NetworkStream? serverStream;

    private readonly SemaphoreSlim clientSemaphore = new(1);
    private readonly SemaphoreSlim serverSemaphore = new(1);

    private Brokefish? brokefish;

    public async Task StartAsync(CancellationToken cancellationToken = default) {
        await this.serverTcp.ConnectAsync(originalEndpoint, cancellationToken).ConfigureAwait(false);
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

        await Task.WhenAny(clientToServer, serverToClient).ConfigureAwait(false);
    }

    private async Task Proxy(
        NetworkStream src, NetworkStream dest,
        SemaphoreSlim semaphore, DestinationType destinationType,
        CancellationToken cancellationToken = default
    ) {
        // Constant buffers that we re-use to be speedy
        var readBuffer = new byte[IConnection.BufferSize].AsMemory();
        var writeBuffer = new byte[IConnection.BufferSize].AsMemory();

        while (src.CanRead && dest.CanWrite && !cancellationToken.IsCancellationRequested) {
            await src.ReadExactlyAsync(readBuffer[..FrameHeader.StructSize], cancellationToken);

            var writePos = FrameHeader.StructSize;

            {
                // Read the rest of the frame
                ref var frameHeader = ref MemoryMarshal.AsRef<FrameHeader>(readBuffer.Span);
                var size = (int) frameHeader.Size;
                var span = readBuffer[FrameHeader.StructSize..size];
                await src.ReadExactlyAsync(span, cancellationToken);
            }

            {
                // This is done twice because we can't bring the `ref` across async boundaries
                ref var readFrameHeader = ref MemoryMarshal.AsRef<FrameHeader>(readBuffer.Span);

                readBuffer.Span[..FrameHeader.StructSize].CopyTo(writeBuffer.Span); // Copy to write buffer

                // Loop for segments
                var readPos = FrameHeader.StructSize;
                var writeCount = (ushort) 0;
                for (var i = 0; i < readFrameHeader.Count; i++) {
                    var segmentHeaderSpan = readBuffer.Span[readPos..(readPos + SegmentHeader.StructSize)];
                    // Copy to write buffer
                    segmentHeaderSpan.CopyTo(writeBuffer.Span[writePos..(writePos + SegmentHeader.StructSize)]);

                    ref var segmentHeader = ref MemoryMarshal.AsRef<SegmentHeader>(segmentHeaderSpan);
                    var segmentSize = (int) segmentHeader.Size;

                    var segmentDataLen = (int) segmentHeader.Size - SegmentHeader.StructSize;
                    var readSegmentDataStart = readPos + SegmentHeader.StructSize;
                    var readSegmentDataEnd = readSegmentDataStart + segmentDataLen;
                    var segmentData = readBuffer[readSegmentDataStart..readSegmentDataEnd];

                    // Not using readPos anymore so we can increment it
                    readPos += segmentSize;

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
                        var packetSegment = new PacketSegment() {
                            FrameHeader = ref readFrameHeader,
                            SegmentHeader = ref segmentHeader,
                            Data = segmentData
                        };
                        this.OnPacketSegmentReceived?.Invoke(ref packetSegment, destinationType, ref segmentDropped);
                    } catch {
                        // ignored
                    }

                    if (isIpc && !segmentDropped) {
                        ref var ipcHeader =
                            ref MemoryMarshal.AsRef<IpcHeader>(segmentData.Span[..IpcHeader.StructSize]);
                        var ipcDataStart = IpcHeader.StructSize;
                        var ipcDataLen = segmentSize - IpcHeader.StructSize - SegmentHeader.StructSize;
                        var ipcDataEnd = ipcDataStart + ipcDataLen;
                        var ipcData = segmentData[ipcDataStart..ipcDataEnd];

                        try {
                            var ipcPacket = new IpcPacket() {
                                FrameHeader = ref readFrameHeader,
                                SegmentHeader = ref segmentHeader,
                                IpcHeader = ref ipcHeader,
                                Data = ipcData
                            };
                            this.OnIpcPacketReceived?.Invoke(ref ipcPacket, destinationType, ref segmentDropped);
                        } catch {
                            // ignored
                        }
                    }

                    if (isEncrypted) this.brokefish!.EncipherPadded(segmentData.Span);

                    if (!segmentDropped) {
                        var segmentDataPos = writePos + SegmentHeader.StructSize;
                        segmentData.CopyTo(writeBuffer[segmentDataPos..(segmentDataPos + segmentDataLen)]);
                        writePos += segmentSize;
                        writeCount++;
                    }
                }

                // Not sending anything, doesn't matter
                if (writeCount == 0) continue;

                // Validate the write frame
                ref var writeFrameHeader =
                    ref MemoryMarshal.AsRef<FrameHeader>(writeBuffer.Span[..FrameHeader.StructSize]);
                writeFrameHeader.Size = writeFrameHeader.DecompressedSize = (uint) writePos;
                writeFrameHeader.Count = writeCount;

                var frameDropped = false;
                try {
                    var packetFrame = new PacketFrame() {
                        FrameHeader = ref writeFrameHeader,
                        Data = writeBuffer[FrameHeader.StructSize..writePos]
                    };
                    this.OnPacketFrameReceived?.Invoke(ref packetFrame, destinationType, ref frameDropped);
                } catch {
                    // ignored
                }
                if (frameDropped) continue;
            }

            // Finally, send it off
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                await dest.WriteAsync(writeBuffer[..writePos], cancellationToken).ConfigureAwait(false);
            } finally {
                semaphore.Release();
            }
        }
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

    public Task SendPacketFrameAsync(
        DestinationType destinationType, FrameHeader frameHeader, ReadOnlyMemory<byte> data
    ) {
        throw new NotImplementedException();
    }

    public Task SendPacketSegmentAsync(
        DestinationType destinationType, SegmentHeader segmentHeader, ReadOnlyMemory<byte> data
    ) {
        throw new NotImplementedException();
    }

    public Task SendIpcPacketAsync(
        DestinationType destinationType, uint sourceActor, uint targetActor,
        IpcHeader ipcHeader, ReadOnlyMemory<byte> data
    ) {
        throw new NotImplementedException();
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
