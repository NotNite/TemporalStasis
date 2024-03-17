using System.Runtime.InteropServices;
using TemporalStasis.Structs;

namespace TemporalStasis.Intercept;

public class IpcInterceptedPacket : IInterceptedPacket {
    public PacketSegmentHeader SegmentHeader;
    public IpcHeader IpcHeader;
    public byte[] Data;
    
    internal IpcInterceptedPacket(RawInterceptedPacket packet) {
        this.SegmentHeader = packet.SegmentHeader;
        this.IpcHeader = packet.Data.ReadStruct<IpcHeader>();
        this.Data = packet.Data[Marshal.SizeOf<IpcHeader>()..];
    }
    
    public IpcInterceptedPacket(PacketSegmentHeader segmentHeader, IpcHeader ipcHeader, byte[] data) {
        this.SegmentHeader = segmentHeader;
        this.IpcHeader = ipcHeader;
        this.Data = data;
    }
    
    public void Revalidate() {
        this.SegmentHeader.Size = (uint) (Marshal.SizeOf<PacketSegmentHeader>() + Marshal.SizeOf<IpcHeader>() + this.Data.Length);
    }
    
    public RawInterceptedPacket ToRawPacket() {
        var data = new byte[Marshal.SizeOf<IpcHeader>() + this.Data.Length];
        data.WriteStruct(this.IpcHeader);
        Array.Copy(this.Data, 0, data, Marshal.SizeOf<IpcHeader>(), this.Data.Length);
        return new RawInterceptedPacket(this.SegmentHeader, data);
    }
}
