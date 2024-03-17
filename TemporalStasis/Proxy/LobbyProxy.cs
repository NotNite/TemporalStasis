using System.Net;
using System.Net.Sockets;
using System.Text;
using TemporalStasis.Intercept;
using TemporalStasis.Structs;

namespace TemporalStasis.Proxy;

public class LobbyProxy(
    IPAddress origHost,
    uint origPort,
    IPAddress listenHost,
    uint listenPort
) : IProxy {
    public const uint EnterWorldOpcode = 15;
    public const int EnterWorldPortOffset = 94;
    public const int EnterWorldHostOffset = 96;
    public const int EnterWorldHostSize = 48;
    
    public event IProxy.RawPacketInterceptor? OnRawServerboundPacket;
    public event IProxy.RawPacketInterceptor? OnRawClientboundPacket;
    public event IProxy.IpcPacketInterceptor? OnIpcServerboundPacket;
    public event IProxy.IpcPacketInterceptor? OnIpcClientboundPacket;
    
    private Dictionary<int, LobbyProxyClient> clients = new();
    
    public ZoneProxy? ZoneProxy = null;
    
    public async Task SendRawPacketAsync(int id, RawInterceptedPacket packet, bool serverbound) {
        if (this.clients.TryGetValue(id, out var client)) await client.SendPacketAsync(packet, serverbound);
    }
    
    public async Task SendIpcPacketAsync(int id, IpcInterceptedPacket packet, bool serverbound) {
        if (this.clients.TryGetValue(id, out var client)) await client.SendPacketAsync(packet.ToRawPacket(), serverbound);
    }
    
    public async Task StartAsync(CancellationToken ct = default) {
        var endpoint = new IPEndPoint(listenHost, (int) listenPort);
        var listener = new TcpListener(endpoint);
        listener.Start();
        
        var tasks = new List<Task>();
        while (!ct.IsCancellationRequested) {
            var client = await listener.AcceptTcpClientAsync(ct);
            tasks.Add(Task.Run(() => this.HandleClient(client), ct));
        }
        
        await Task.WhenAll(tasks);
    }
    
    private async Task HandleClient(TcpClient client) {
        await using var stream = client.GetStream();
        using var proxy = new TcpClient();
        await proxy.ConnectAsync(origHost, (int) origPort);
        await using var proxyStream = proxy.GetStream();
        
        var id = proxy.GetHashCode();
        var proxyClient = new LobbyProxyClient(
            id, stream, proxyStream,
            (ref RawInterceptedPacket packet, ref bool dropped, bool serverbound) => {
                try {
                    if (serverbound) {
                        this.OnRawServerboundPacket?.Invoke(id, ref packet, ref dropped, ConnectionType.Lobby);
                    } else {
                        this.OnRawClientboundPacket?.Invoke(id, ref packet, ref dropped, ConnectionType.Lobby);
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            },
            (ref IpcInterceptedPacket packet, ref bool dropped, bool serverbound) => {
                try {
                    if (serverbound) {
                        this.OnIpcServerboundPacket?.Invoke(id, ref packet, ref dropped, ConnectionType.Lobby);
                    } else {
                        this.OnIpcClientboundPacket?.Invoke(id, ref packet, ref dropped, ConnectionType.Lobby);
                        
                        if (this.ZoneProxy is not null && packet.IpcHeader.Opcode == EnterWorldOpcode) {
                            var packetPort = BitConverter.ToUInt16(packet.Data[EnterWorldPortOffset..]);
                            var packetHost = Encoding.UTF8.GetString(
                                packet.Data[EnterWorldHostOffset..(EnterWorldHostOffset + EnterWorldHostSize)]);
                            this.ZoneProxy.NextHost = packetHost;
                            this.ZoneProxy.NextPort = packetPort;
                            
                            var port = BitConverter.GetBytes(this.ZoneProxy.PublicPort);
                            
                            var host = new byte[EnterWorldHostSize];
                            var newHost = Encoding.UTF8.GetBytes(this.ZoneProxy.PublicHost.ToString());
                            if (newHost.Length <= EnterWorldHostSize) {
                                Array.Copy(newHost, host, newHost.Length);
                                packet.Data[EnterWorldPortOffset] = port[0];
                                packet.Data[EnterWorldPortOffset + 1] = port[1];
                                Array.Copy(host, 0, packet.Data, EnterWorldHostOffset, EnterWorldHostSize);
                            }
                            
                            //Console.WriteLine($"EnterWorld packet received, forwarding to {packetHost}:{packetPort}");
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            }
        );
        this.clients[id] = proxyClient;
        
        try {
            await proxyClient.Run();
        } finally {
            client.Close();
            this.clients.Remove(id);
        }
    }
}
