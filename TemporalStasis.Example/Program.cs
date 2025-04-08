using System.Net;
using TemporalStasis;
using TemporalStasis.Compression;

// Connect to Aether with these launch arguments: DEV.LobbyHost04=127.0.0.1 DEV.LobbyPort04=44994
// (see README for more information)
var aether = await Dns.GetHostEntryAsync("neolobby02.ffxiv.com");
var lobbyProxy = new LobbyProxy(
    new IPEndPoint(aether.AddressList[0], 54994), // Original lobby server
    new IPEndPoint(IPAddress.Loopback, 44994)     // Where to listen on
);

// Ensure this is next to the built TemporalStasis.Example.exe, this is required for the zone proxy
// (see README for how to get this)
var oodle = new OodleLibraryFactory("oodle-network-shared.dll");

// The lobby proxy will rewrite clients to go to this zone proxy
// (be careful about multiple clients connecting at once, you will cause a race condition)
var zoneProxy = new ZoneProxy(
    oodle,
    new IPEndPoint(IPAddress.Loopback, 44992) // Where to listen on
);
lobbyProxy.ZoneProxy = zoneProxy;

lobbyProxy.OnClientConnected += (connection) => {
    Console.WriteLine("New connection to the lobby proxy!");

    connection.OnIpcPacketReceived += (ref IpcPacket packet, DestinationType type, ref bool dropped) => {
        Console.WriteLine($"Got lobby IPC packet to {type} with opcode {packet.IpcHeader.Opcode}: "
                          + Convert.ToHexString(packet.Data));

        // You can read and even write to the packet!
        if (packet.IpcHeader.Opcode == 2) {
            var playersInQueue = BitConverter.ToUInt16(packet.Data[12..14]);
            Console.WriteLine("Players in queue: " + playersInQueue);
        }
    };
};

zoneProxy.OnClientConnected += (connection) => {
    Console.WriteLine("New connection to the zone proxy!");

    connection.OnIpcPacketReceived += (ref IpcPacket packet, DestinationType type, ref bool dropped) => {
        // This is either the zone connection or chat connection
        var connType = connection.Type?.ToString() ?? "unknown";

        Console.WriteLine($"Got {connType} IPC packet to {type} with opcode {packet.IpcHeader.Opcode}: "
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
