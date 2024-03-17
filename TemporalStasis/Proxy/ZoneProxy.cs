using System.Net;
using System.Net.Sockets;
using TemporalStasis.Compression;
using TemporalStasis.Intercept;
using TemporalStasis.Structs;

namespace TemporalStasis.Proxy;

public class ZoneProxy(
    IOodleFactory oodleFactory,
    IPAddress listenHost,
    uint listenPort,
    IPAddress? publicHost = null,
    uint? publicPort = null
) : IProxy {
    public IPAddress PublicHost => publicHost ?? listenHost;
    public uint PublicPort => publicPort ?? listenPort;
    
    public string? NextHost;
    public uint? NextPort;
    private Dictionary<int, ZoneProxyClient> clients = new();
    
    public event IProxy.RawPacketInterceptor? OnRawServerboundPacket;
    public event IProxy.RawPacketInterceptor? OnRawClientboundPacket;
    public event IProxy.IpcPacketInterceptor? OnIpcServerboundPacket;
    public event IProxy.IpcPacketInterceptor? OnIpcClientboundPacket;
    
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
        if (this.NextHost is null || this.NextPort is null) {
            client.Close();
            return;
        }
        
        await using var stream = client.GetStream();
        using var proxy = new TcpClient();
        await proxy.ConnectAsync(this.NextHost, (int) this.NextPort);
        await using var proxyStream = proxy.GetStream();
        
        var id = proxy.GetHashCode();
        var proxyClient = new ZoneProxyClient(
            oodleFactory, id, stream, proxyStream,
            (ref RawInterceptedPacket packet, ref bool dropped, bool serverbound, ConnectionType type) => {
                try {
                    if (serverbound) {
                        this.OnRawServerboundPacket?.Invoke(id, ref packet, ref dropped, type);
                    } else {
                        this.OnRawClientboundPacket?.Invoke(id, ref packet, ref dropped, type);
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            },
            (ref IpcInterceptedPacket packet, ref bool dropped, bool serverbound, ConnectionType type) => {
                try {
                    if (serverbound) {
                        this.OnIpcServerboundPacket?.Invoke(id, ref packet, ref dropped, type);
                    } else {
                        this.OnIpcClientboundPacket?.Invoke(id, ref packet, ref dropped, type);
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
