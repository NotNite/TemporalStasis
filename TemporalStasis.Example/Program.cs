using System.Net;
using TemporalStasis.Compression;
using TemporalStasis.Intercept;
using TemporalStasis.Proxy;
using TemporalStasis.Structs;

// Connect to Aether with these launch arguments: DEV.LobbyHost04=127.0.0.1 DEV.LobbyPort04=44994
var aether = await Dns.GetHostEntryAsync("neolobby02.ffxiv.com");
var addr = aether.AddressList[0];
var lobbyProxy = new LobbyProxy(addr, 54994, IPAddress.Loopback, 44994);

var oodle = new OodleLibraryFactory("oodle-network-shared.dll");

var zoneProxy = new ZoneProxy(oodle, IPAddress.Loopback, 44992);
lobbyProxy.ZoneProxy = zoneProxy;

void LobbyIpcClientboundPacket(int id, ref IpcInterceptedPacket packet, ref bool dropped, ConnectionType type) {
    if (packet.IpcHeader.Opcode == 2) {
        Console.WriteLine("Lobby queue status received...");
        var playersInQueue = BitConverter.GetBytes((ushort) 42069);
        packet.Data[12] = playersInQueue[0];
        packet.Data[13] = playersInQueue[1];
    }
}

void ZoneIpcClientboundPacket(int id, ref IpcInterceptedPacket packet, ref bool dropped, ConnectionType type) {
    if (type != ConnectionType.Zone) return;
    Console.WriteLine("Received zone IPC packet with opcode " + packet.IpcHeader.Opcode);
}

lobbyProxy.OnIpcClientboundPacket += LobbyIpcClientboundPacket;
zoneProxy.OnIpcClientboundPacket += ZoneIpcClientboundPacket;

await Task.WhenAll(lobbyProxy.StartAsync(), zoneProxy.StartAsync());
