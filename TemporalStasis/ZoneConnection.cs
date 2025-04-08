using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using TemporalStasis.Compression;
using TemporalStasis.Structs;

namespace TemporalStasis;

internal sealed class ZoneConnection(
    TcpClient clientTcp,
    IPEndPoint originalEndpoint,
    IOodleFactory oodleFactory
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
        // Oodle might be able to decompress/recompress in place but I'm not too sure
        var oodleBuffer = new byte[IConnection.BufferSize].AsMemory();
        var readBuffer = new byte[IConnection.BufferSize].AsMemory();
        var writeBuffer = new byte[IConnection.BufferSize].AsMemory();

        using var readOodle = oodleFactory.Create();
        using var writeOodle = oodleFactory.Create();

        while (src.CanRead && dest.CanWrite && !cancellationToken.IsCancellationRequested) {
            await src.ReadExactlyAsync(readBuffer[..FrameHeader.StructSize], cancellationToken);

            var writePos = FrameHeader.StructSize;

            {
                // Read the rest of the frame
                ref var frameHeader = ref MemoryMarshal.AsRef<FrameHeader>(readBuffer.Span);

                var size = (int) frameHeader.Size;
                var compressionType = frameHeader.CompressionType;
                var decompressedSize = frameHeader.DecompressedSize;
                if (this.Type is null && frameHeader.ConnectionType != ConnectionType.None) {
                    this.Type = frameHeader.ConnectionType;
                }

                if (compressionType is CompressionType.Oodle) {
                    var span = oodleBuffer[..(size - FrameHeader.StructSize)];
                    await src.ReadExactlyAsync(span, cancellationToken);

                    await semaphore.WaitAsync(cancellationToken);
                    try {
                        readOodle.Decompress(span.Span,
                            readBuffer.Span[FrameHeader.StructSize..],
                            (int) decompressedSize);
                    } finally {
                        semaphore.Release();
                    }
                } else {
                    var span = readBuffer[FrameHeader.StructSize..size];
                    await src.ReadExactlyAsync(span, cancellationToken);
                }
            }

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

                var isIpc = segmentHeader.SegmentType is SegmentType.Ipc;
                var segmentDropped = false;
                try {
                    var packetSegment = new PacketSegment() {
                        FrameHeader = ref readFrameHeader,
                        SegmentHeader = ref segmentHeader,
                        Data = segmentData.Span
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
                            Data = ipcData.Span
                        };
                        this.OnIpcPacketReceived?.Invoke(ref ipcPacket, destinationType, ref segmentDropped);
                    } catch {
                        // ignored
                    }
                }

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
            writeFrameHeader.Size = (uint) writePos;
            writeFrameHeader.DecompressedSize = writeFrameHeader.Size - (uint) FrameHeader.StructSize;
            writeFrameHeader.Count = writeCount;

            var frameDropped = false;
            var writeData = writeBuffer[FrameHeader.StructSize..writePos];
            try {
                var packetFrame = new PacketFrame() {
                    FrameHeader = ref writeFrameHeader,
                    Data = writeData.Span
                };
                this.OnPacketFrameReceived?.Invoke(ref packetFrame, destinationType, ref frameDropped);
            } catch {
                // ignored
            }
            if (frameDropped) continue;

            if (writeFrameHeader.CompressionType is CompressionType.Oodle) {
                // Re-compress
                await semaphore.WaitAsync(cancellationToken);
                try {
                    var size = writeOodle.Compress(writeData.Span, oodleBuffer.Span);
                    writeFrameHeader.Size = (uint) (FrameHeader.StructSize + size);

                    await dest.WriteAsync(writeBuffer[..FrameHeader.StructSize], cancellationToken);
                    await dest.WriteAsync(oodleBuffer[..size], cancellationToken);
                } finally {
                    semaphore.Release();
                }
            } else {
                // Send off normally
                await semaphore.WaitAsync(cancellationToken);
                try {
                    await dest.WriteAsync(writeBuffer[..writePos], cancellationToken);
                } finally {
                    semaphore.Release();
                }
            }
        }
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
