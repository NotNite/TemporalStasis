namespace TemporalStasis;

/// <summary>A generic proxy for a FFXIV server.</summary>
public interface IProxy : IDisposable {
    /// <summary>Fired when a game client connects to the proxy. See <see cref="IConnection"/>.</summary>
    public delegate void ClientConnectedDelegate(IConnection connection);

    /// <summary>Fired when a game client disconnects from the proxy.</summary>
    public delegate void ClientDisconnectedDelegate(IConnection connection);

    /// <inheritdoc cref="ClientConnectedDelegate"/>
    public event ClientConnectedDelegate? OnClientConnected;

    /// <inheritdoc cref="ClientDisconnectedDelegate"/>
    public event ClientDisconnectedDelegate? OnClientDisconnected;

    /// <summary>Start and run the proxy, continuing forever until it is cancelled.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default);
}
