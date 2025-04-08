using System.Net;
using System.Net.Sockets;
using TemporalStasis.Compression;

namespace TemporalStasis;

public class ZoneProxy : IZoneProxy {
    public event IProxy.ClientConnectedDelegate? OnClientConnected;
    public event IProxy.ClientDisconnectedDelegate? OnClientDisconnected;

    public IPEndPoint PublicEndpoint { get; }

    private readonly IOodleFactory oodleFactory;
    private readonly IPEndPoint listenEndpoint;
    private readonly TcpListener listener;
    private IPEndPoint? nextServer;

    /// <param name="oodleFactory">A factory for <see cref="IOodle"/> instances.</param>
    /// <param name="listenEndpoint">The endpoint the proxy will listen on.</param>
    /// <param name="publicEndpoint">The public endpoint of the zone proxy. <seealso cref="IZoneProxy.PublicEndpoint"/></param>
    public ZoneProxy(
        IOodleFactory oodleFactory,
        IPEndPoint listenEndpoint,
        IPEndPoint? publicEndpoint = null
    ) {
        this.oodleFactory = oodleFactory;
        this.listenEndpoint = listenEndpoint;
        this.PublicEndpoint = publicEndpoint ?? this.listenEndpoint;
        this.listener = new TcpListener(this.listenEndpoint);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default) {
        this.listener.Start();

        var tasks = new List<Task>();
        while (!cancellationToken.IsCancellationRequested) {
            var client = await this.listener.AcceptTcpClientAsync(cancellationToken);
            tasks.Add(Task.Run(() => this.HandleConnection(client, cancellationToken), cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private async Task HandleConnection(TcpClient client, CancellationToken cancellationToken = default) {
        // nextServer can't be reset here since there are two connections (zone and chat)
        if (this.nextServer is null) throw new Exception("Connection received without next server specified");
        using var connection = new ZoneConnection(client, this.nextServer, this.oodleFactory);
        this.OnClientConnected?.Invoke(connection);
        await Util.WrapTcpErrors(() => connection.StartAsync(cancellationToken));
        this.OnClientDisconnected?.Invoke(connection);
    }

    public void SetNextServer(IPEndPoint server)
        => this.nextServer = server;

    public void Dispose() {
        this.listener.Stop();
        this.listener.Dispose();
    }
}
