using TemporalStasis.Intercept;
using TemporalStasis.Structs;

namespace TemporalStasis.Proxy;

public interface IProxy {
    public delegate void RawPacketInterceptor(int id, ref RawInterceptedPacket packet, ref bool dropped, ConnectionType type);
    public delegate void IpcPacketInterceptor(int id, ref IpcInterceptedPacket packet, ref bool dropped, ConnectionType type);
    
    public event RawPacketInterceptor? OnRawServerboundPacket;
    public event RawPacketInterceptor? OnRawClientboundPacket;
    public event IpcPacketInterceptor? OnIpcServerboundPacket;
    public event IpcPacketInterceptor? OnIpcClientboundPacket;
    
    public Task SendRawPacketAsync(int id, RawInterceptedPacket packet, bool serverbound);
    public Task SendIpcPacketAsync(int id, IpcInterceptedPacket packet, bool serverbound);
    
    public Task StartAsync(CancellationToken ct = default);
}
