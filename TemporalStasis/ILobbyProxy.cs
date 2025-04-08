namespace TemporalStasis;

/// <summary>A generic proxy for the FFXIV lobby server.</summary>
public interface ILobbyProxy : IProxy {
    /// <inheritdoc cref="LobbyProxyConfig"/>
    public LobbyProxyConfig Config { get; set; }
}
