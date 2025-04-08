using System.Net;
using System.Net.Sockets;

namespace TemporalStasis;

/// <summary>A proxy for the FFXIV lobby server.</summary>
public sealed class LobbyProxy : ILobbyProxy {
    public event IProxy.ClientConnectedDelegate? OnClientConnected;
    public event IProxy.ClientDisconnectedDelegate? OnClientDisconnected;

    public LobbyProxyConfig Config { get; set; } = new();

    private readonly IPEndPoint originalEndpoint;
    private readonly IPEndPoint listenEndpoint;
    private readonly TcpListener listener;

    /// <param name="originalEndpoint">
    /// The endpoint of the original lobby server, where connections will be proxied to.
    /// </param>
    /// <param name="listenEndpoint">
    /// The endpoint the proxy will listen on, where clients will connect to.
    /// </param>
    public LobbyProxy(IPEndPoint originalEndpoint, IPEndPoint listenEndpoint) {
        this.originalEndpoint = originalEndpoint;
        this.listenEndpoint = listenEndpoint;
        this.listener = new TcpListener(this.listenEndpoint);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default) {
        this.listener.Start();

        var tasks = new List<Task>();
        while (!cancellationToken.IsCancellationRequested) {
            var client = await this.listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            tasks.Add(Task.Run(() => this.HandleConnection(client, cancellationToken), cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task HandleConnection(TcpClient client, CancellationToken cancellationToken = default) {
        using var connection = new LobbyConnection(client, this.originalEndpoint, this.Config);
        this.OnClientConnected?.Invoke(connection);
        await Util.WrapTcpErrors(() => connection.StartAsync(cancellationToken));
        this.OnClientDisconnected?.Invoke(connection);
    }

    public void Dispose() {
        this.listener.Stop();
        this.listener.Dispose();
    }
}
