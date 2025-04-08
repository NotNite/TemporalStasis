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
        Memory<byte> oodleBuffer = new byte[IConnection.BufferSize];
        Memory<byte> readBuffer = new byte[IConnection.BufferSize];
        Memory<byte> writeBuffer = new byte[IConnection.BufferSize];

        using var readOodle = oodleFactory.Create();
        using var writeOodle = oodleFactory.Create();

        while (src.CanRead && dest.CanWrite && !cancellationToken.IsCancellationRequested) {
            bool isOodle;

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
                var decompressedSize = (int) frameHeader.DecompressedSize;
                isOodle = frameHeader.CompressionType is CompressionType.Oodle;

                if (isOodle) {
                    // Read into a separate buffer
                    var oodleMemory = oodleBuffer[..(size - FrameHeader.StructSize)];
                    await src.ReadExactlyAsync(oodleMemory, cancellationToken);

                    await semaphore.WaitAsync(cancellationToken);
                    try {
                        // Decompress into original buffer
                        readOodle.Decompress(
                            oodleMemory.Span,
                            readBuffer.Span.Slice(FrameHeader.StructSize, decompressedSize),
                            decompressedSize
                        );
                    } finally {
                        semaphore.Release();
                    }
                } else {
                    // Read directly into the buffer
                    await src.ReadExactlyAsync(readBuffer[FrameHeader.StructSize..size], cancellationToken);
                }
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

                var isIpc = segmentHeader.SegmentType is SegmentType.Ipc;
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

            // Finally, send it off
            await semaphore.WaitAsync(cancellationToken);
            try {
                if (isOodle) {
                    // Recompress into another buffer
                    var oodleData = writeBuffer[FrameHeader.StructSize..writePos];
                    var compressedSize = writeOodle.Compress(oodleData.Span, oodleBuffer.Span);
                    writeFrameHeader.Size = (uint) (FrameHeader.StructSize + compressedSize);

                    await dest.WriteAsync(writeBuffer[..FrameHeader.StructSize], cancellationToken);
                    await dest.WriteAsync(oodleBuffer[..compressedSize], cancellationToken);
                } else {
                    // Write like normal
                    await dest.WriteAsync(writeBuffer[..writePos], cancellationToken);
                }
            } finally {
                semaphore.Release();
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
