namespace TemporalStasis;

/// <summary>A generic proxy for the FFXIV lobby server.</summary>
public interface ILobbyProxy : IProxy {
    /// <inheritdoc cref="LobbyProxyConfig"/>
    public LobbyProxyConfig Config { get; set; }

    /// <summary>The zone proxy to forward logins to.</summary>
    public IZoneProxy? ZoneProxy { get; set; }
}
