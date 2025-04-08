using System.Net;
using TemporalStasis;
using TemporalStasis.Compression;
using TemporalStasis.Structs;

// Ensure this is next to the built .exe
var oodle = new OodleLibraryFactory("oodle-network-shared.dll");

// Connect to Aether with these launch arguments: DEV.LobbyHost04=127.0.0.1 DEV.LobbyPort04=44994
var aether = await Dns.GetHostEntryAsync("neolobby02.ffxiv.com");
var lobbyProxy = new LobbyProxy(
    new IPEndPoint(aether.AddressList[0], 54994),
    new IPEndPoint(IPAddress.Loopback, 44994)
);

lobbyProxy.OnClientConnected += (connection) => {
    Console.WriteLine("New connection to the lobby proxy!");

    connection.OnPacketFrameReceived += (ref PacketFrame frame, DestinationType _, ref bool _) => {
        // Console.WriteLine("Raw frame data received: " + Convert.ToHexString(frame.Data));
    };

    connection.OnIpcPacketReceived += (ref IpcPacket packet, DestinationType destinationType, ref bool _) => {
        Console.WriteLine(
            $"Got IPC packet to {destinationType} with opcode {packet.IpcHeader.Opcode}: {Convert.ToHexString(packet.Data.Span)}");

        if (packet.IpcHeader.Opcode == 2) {
            var playersInQueue = BitConverter.ToUInt16(packet.Data.Span[12..14]);
            Console.WriteLine("Players in queue: " + playersInQueue);
        }
    };
};

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, _) => {
    Console.WriteLine("Shutting down...");
    cts.Cancel();
};

Console.WriteLine("Starting proxies...");
await Task.WhenAll(
    Task.Run(() => lobbyProxy.StartAsync(cts.Token), cts.Token)
);
