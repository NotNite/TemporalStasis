using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TemporalStasis.Compression;
using TemporalStasis.Encryption;
using TemporalStasis.Intercept;
using TemporalStasis.Structs;

namespace TemporalStasis.Proxy;

public class ZoneProxyClient {
    public delegate void RawFrameClientInterceptor(byte[] frame, bool serverbound);

    public delegate void
        RawPacketClientInterceptor(
            ref RawInterceptedPacket packet, ref bool dropped, bool serverbound, ConnectionType type
        );

    public delegate void
        IpcPacketClientInterceptor(
            ref IpcInterceptedPacket packet, ref bool dropped, bool serverbound, ConnectionType type
        );

    public int Id;

    private IOodleFactory oodleFactory;
    private NetworkStream stream;
    private NetworkStream proxyStream;
    private RawFrameClientInterceptor frame;
    private RawPacketClientInterceptor raw;
    private IpcPacketClientInterceptor ipc;
    private SemaphoreSlim semaphore = new(1);
    private ConnectionType type = ConnectionType.None;

    private IOodle? clientboundCompressor;
    private IOodle? serverboundCompressor;

    public ZoneProxyClient(
        IOodleFactory oodleFactory,
        int id,
        NetworkStream stream,
        NetworkStream proxyStream,
        RawFrameClientInterceptor frame,
        RawPacketClientInterceptor raw,
        IpcPacketClientInterceptor ipc
    ) {
        this.oodleFactory = oodleFactory;
        this.Id = id;
        this.stream = stream;
        this.proxyStream = proxyStream;
        this.frame = frame;
        this.raw = raw;
        this.ipc = ipc;
    }

    public async Task Run() {
        var clientToProxy = this.Proxy(this.stream, this.proxyStream, true);
        var proxyToClient = this.Proxy(this.proxyStream, this.stream, false);

        try {
            await Task.WhenAll(clientToProxy, proxyToClient);
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }

    public async Task SendPacketAsync(RawInterceptedPacket packet, bool serverbound) {
        byte[] data;
        using (var ms = new MemoryStream()) {
            await ms.WriteStructAsync(packet.SegmentHeader);
            await ms.WriteBytesAsync(packet.Data);
            data = ms.ToArray();
        }

        var compressor = serverbound ? this.serverboundCompressor : this.clientboundCompressor;
        var oodled = compressor!.Encode(data);
        await this.semaphore.WaitAsync();
        try {
            var header = new PacketHeader {
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Size = (uint) (Marshal.SizeOf<PacketHeader>() + oodled.Length),
                ConnectionType = this.type,
                Count = 1,
                CompressionType = CompressionType.Oodle,
                UncompressedSize = (uint) data.Length
            };

            var target = serverbound ? this.stream : this.proxyStream;
            await target.WriteStructAsync(header);
            await target.WriteBytesAsync(oodled);
        } finally {
            this.semaphore.Release();
        }
    }

    private async Task Proxy(NetworkStream src, NetworkStream dest, bool serverbound) {
        using var oodle = this.oodleFactory.Create();
        using var otherOodle = this.oodleFactory.Create();
        if (serverbound) {
            this.serverboundCompressor = oodle;
        } else {
            this.clientboundCompressor = oodle;
        }

        while (src.CanRead && dest.CanWrite) {
            var header = await src.ReadStructAsync<PacketHeader>();
            if (header.ConnectionType != ConnectionType.None) {
                this.type = header.ConnectionType;
            }

            var data = await src.ReadBytesAsync((int) (header.Size - Marshal.SizeOf<PacketHeader>()));

            if (header.CompressionType == CompressionType.Oodle) {
                data = oodle.Decode(data, (int) header.UncompressedSize);
            }

            using (var ms = new MemoryStream(data)) {
                var survivedPackets = new List<RawInterceptedPacket>();
                for (var i = 0; i < header.Count; i++) {
                    var packet = new RawInterceptedPacket(ms);

                    var dropped = false;
                    this.raw.Invoke(ref packet, ref dropped, serverbound, this.type);

                    if (packet.SegmentHeader.SegmentType == SegmentType.Ipc) {
                        var ipcPacket = new IpcInterceptedPacket(packet);
                        this.ipc.Invoke(ref ipcPacket, ref dropped, serverbound, this.type);
                        ipcPacket.Revalidate();
                        packet = ipcPacket.ToRawPacket();
                    }

                    if (!dropped) survivedPackets.Add(packet);
                }

                var headerSize = (uint) Marshal.SizeOf<PacketHeader>();
                foreach (var packet in survivedPackets) {
                    packet.Revalidate();
                    headerSize += packet.SegmentHeader.Size;
                }

                header.Size = headerSize;
                header.Count = (ushort) survivedPackets.Count;

                using var newData = new MemoryStream();
                foreach (var packet in survivedPackets) {
                    await newData.WriteStructAsync(packet.SegmentHeader);
                    await newData.WriteBytesAsync(packet.Data);
                    await newData.FlushAsync();
                }

                data = newData.ToArray();
            }

            var newHeader = new PacketHeader {
                Unknown0 = header.Unknown0,
                Unknown8 = header.Unknown8,
                Timestamp = header.Timestamp,
                Size = (uint) (Marshal.SizeOf<PacketHeader>() + data.Length),
                ConnectionType = this.type,
                Count = header.Count,
                CompressionType = CompressionType.None,
                Unknown24 = header.Unknown24,
                UncompressedSize = (uint) data.Length
            };
            using (var frameStream = new MemoryStream()) {
                await frameStream.WriteStructAsync(newHeader);
                await frameStream.WriteBytesAsync(data);
                await frameStream.FlushAsync();
                this.frame.Invoke(frameStream.ToArray(), serverbound);
            }

            if (header.CompressionType == CompressionType.Oodle) {
                header.UncompressedSize = (uint) data.Length;
                data = otherOodle.Encode(data);
                header.Size = (uint) (Marshal.SizeOf<PacketHeader>() + data.Length);
            }

            await this.semaphore.WaitAsync();
            try {
                await dest.WriteStructAsync(header);
                await dest.WriteBytesAsync(data);
            } finally {
                this.semaphore.Release();
            }
        }
    }
}
