using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TemporalStasis.Encryption;
using TemporalStasis.Intercept;
using TemporalStasis.Structs;

namespace TemporalStasis.Proxy;

public class LobbyProxyClient {
    public delegate void RawFrameClientInterceptor(byte[] frame, bool serverbound);

    public delegate void RawPacketClientInterceptor(
        ref RawInterceptedPacket packet, ref bool dropped, bool serverbound
    );

    public delegate void
        IpcPacketClientInterceptor(ref IpcInterceptedPacket packet, ref bool dropped, bool serverbound);

    public int Id;
    private Brokefish? brokefish;

    private NetworkStream stream;
    private NetworkStream proxyStream;
    private RawFrameClientInterceptor frame;
    private RawPacketClientInterceptor raw;
    private IpcPacketClientInterceptor ipc;
    private SemaphoreSlim semaphore = new(1);

    public LobbyProxyClient(
        int id,
        NetworkStream stream,
        NetworkStream proxyStream,
        RawFrameClientInterceptor frame,
        RawPacketClientInterceptor raw,
        IpcPacketClientInterceptor ipc
    ) {
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
        if (this.brokefish is not null && packet.SegmentHeader.SegmentType == SegmentType.Ipc) {
            this.brokefish.Encipher(packet.Data, 0, packet.Data.Length);
        }

        await this.semaphore.WaitAsync();

        try {
            var size = (uint) (Marshal.SizeOf<PacketHeader>() + packet.SegmentHeader.Size);
            var header = new PacketHeader {
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Size = size,
                ConnectionType = ConnectionType.Lobby,
                Count = 1,
                CompressionType = CompressionType.None,
                UncompressedSize = size
            };

            var target = serverbound ? this.stream : this.proxyStream;
            await target.WriteStructAsync(header);
            await target.WriteStructAsync(packet.SegmentHeader);
            await target.WriteBytesAsync(packet.Data);
        } finally {
            this.semaphore.Release();
        }
    }

    private async Task Proxy(NetworkStream src, NetworkStream dest, bool serverbound) {
        while (src.CanRead && dest.CanWrite) {
            var header = await src.ReadStructAsync<PacketHeader>();
            var survivedPackets = new List<RawInterceptedPacket>();

            using var rawPacket = new MemoryStream();
            var rawPacketSize = 0;
            rawPacket.WriteStruct(header);

            for (var i = 0; i < header.Count; i++) {
                var packet = new RawInterceptedPacket(src);

                if (packet.SegmentHeader.SegmentType == SegmentType.EncryptionInit && serverbound) {
                    var key = BitConverter.ToUInt32(packet.Data, 100);
                    var keyPhrase = packet.Data[36..(36 + 32)];
                    var baseKey = new byte[0x2c];

                    baseKey[0] = 0x78;
                    baseKey[1] = 0x56;
                    baseKey[2] = 0x34;
                    baseKey[3] = 0x12;
                    BitConverter.GetBytes(key).CopyTo(baseKey, 4);

                    // Game version, this will need changing in the future (7000)
                    baseKey[8] = 0x58;
                    baseKey[9] = 0x1b;
                    keyPhrase.CopyTo(baseKey, 0xc);

                    var encKey = MD5.Create().ComputeHash(baseKey);
                    this.brokefish = new Brokefish(encKey);
                }

                // Blowfish only applies to IPC packets
                var shouldRecrypt = this.brokefish is not null && packet.SegmentHeader.SegmentType == SegmentType.Ipc;

                // Slow, but it works for now
                if (shouldRecrypt) {
                    this.brokefish!.Decipher(packet.Data, 0, packet.Data.Length);
                }

                var dropped = false;
                this.raw.Invoke(ref packet, ref dropped, serverbound);

                if (packet.SegmentHeader.SegmentType == SegmentType.Ipc) {
                    var ipcPacket = new IpcInterceptedPacket(packet);

                    this.ipc.Invoke(ref ipcPacket, ref dropped, serverbound);

                    ipcPacket.Revalidate();
                    packet = ipcPacket.ToRawPacket();
                }

                packet.Revalidate();
                await rawPacket.WriteStructAsync(packet.SegmentHeader);
                await rawPacket.WriteBytesAsync(packet.Data);
                await rawPacket.FlushAsync();
                rawPacketSize += (int) packet.SegmentHeader.Size;

                if (shouldRecrypt) {
                    this.brokefish!.Encipher(packet.Data, 0, packet.Data.Length);
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
            header.UncompressedSize = headerSize;

            // Remake the header
            rawPacket.Seek(0, SeekOrigin.Begin);
            var newHeader = await rawPacket.ReadStructAsync<PacketHeader>();
            newHeader.Size = (uint) (Marshal.SizeOf<PacketHeader>() + rawPacketSize);
            newHeader.UncompressedSize = (uint) rawPacketSize;
            newHeader.CompressionType = CompressionType.None;

            rawPacket.Seek(0, SeekOrigin.Begin);
            await rawPacket.WriteStructAsync(newHeader);
            await rawPacket.FlushAsync();

            this.frame.Invoke(rawPacket.ToArray(), serverbound);

            await this.semaphore.WaitAsync();
            try {
                await dest.WriteStructAsync(header);
                foreach (var packet in survivedPackets) {
                    dest.WriteStruct(packet.SegmentHeader);
                    await dest.WriteBytesAsync(packet.Data);
                }
            } finally {
                this.semaphore.Release();
            }
        }
    }
}
