namespace TemporalStasis;

/// <summary>A generic proxy for a FFXIV server.</summary>
public interface IProxy : IDisposable {
    public delegate void ClientConnectedDelegate(IConnection connection);
    public delegate void ClientDisconnectedDelegate(IConnection connection);

    public event ClientConnectedDelegate? OnClientConnected;
    public event ClientDisconnectedDelegate? OnClientDisconnected;

    /// <summary>Start and run the proxy, continuing forever until it is cancelled.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default);
}
