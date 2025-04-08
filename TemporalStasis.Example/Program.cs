using System.Net;
using TemporalStasis;
using TemporalStasis.Compression;

// Connect to Aether with these launch arguments: DEV.LobbyHost04=127.0.0.1 DEV.LobbyPort04=44994
var aether = await Dns.GetHostEntryAsync("neolobby02.ffxiv.com");
var lobbyProxy = new LobbyProxy(
    new IPEndPoint(aether.AddressList[0], 54994),
    new IPEndPoint(IPAddress.Loopback, 44994)
);

// Ensure this is next to the built TemporalStasis.Example.exe
var oodle = new OodleLibraryFactory("oodle-network-shared.dll");

// The lobby proxy will rewrite clients to go to this zone proxy
// (be careful about multiple clients connecting at once, you will cause a race condition)
var zoneProxy = new ZoneProxy(
    oodle,
    new IPEndPoint(IPAddress.Loopback, 44992)
);
lobbyProxy.ZoneProxy = zoneProxy;

lobbyProxy.OnClientConnected += (connection) => {
    Console.WriteLine("New connection to the lobby proxy!");

    // You can also receive on packet frames and packet segments! In Lobby, packet frames will still be encrypted
    connection.OnIpcPacketReceived += (ref IpcPacket packet, DestinationType destinationType, ref bool _) => {
        Console.WriteLine($"Got lobby IPC packet to {destinationType} with opcode {packet.IpcHeader.Opcode}: "
                          + Convert.ToHexString(packet.Data));

        if (packet.IpcHeader.Opcode == 2) {
            var playersInQueue = BitConverter.ToUInt16(packet.Data[12..14]);
            Console.WriteLine("Players in queue: " + playersInQueue);
        }
    };
};

zoneProxy.OnClientConnected += (connection) => {
    Console.WriteLine("New connection to the zone proxy!");

    connection.OnIpcPacketReceived += (ref IpcPacket packet, DestinationType destinationType, ref bool _) => {
        Console.WriteLine($"Got zone IPC packet to {destinationType} with opcode {packet.IpcHeader.Opcode}: "
                          + Convert.ToHexString(packet.Data));
    };
};

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => {
    Console.WriteLine("Shutting down...");
    cts.Cancel();
};

Console.WriteLine("Starting proxies...");
await Task.WhenAll(
    Task.Run(() => lobbyProxy.StartAsync(cts.Token), cts.Token),
    Task.Run(() => zoneProxy.StartAsync(cts.Token), cts.Token)
);
