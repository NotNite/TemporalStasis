using System.Runtime.InteropServices;
using TemporalStasis.Structs;

namespace TemporalStasis.Intercept;

public class RawInterceptedPacket : IInterceptedPacket {
    public PacketSegmentHeader SegmentHeader;
    public byte[] Data;

    internal RawInterceptedPacket(Stream client) {
        this.SegmentHeader = client.ReadStruct<PacketSegmentHeader>();
        this.Data = client.ReadBytes((int) (this.SegmentHeader.Size - Marshal.SizeOf<PacketSegmentHeader>()));
    }
    
    public RawInterceptedPacket(PacketSegmentHeader segmentHeader, byte[] data) {
        this.SegmentHeader = segmentHeader;
        this.Data = data;
    }

    public void Revalidate() {
        this.SegmentHeader.Size = (uint) (Marshal.SizeOf<PacketSegmentHeader>() + this.Data.Length);
    }
}
