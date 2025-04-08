using System.Net;

namespace TemporalStasis;

/// <summary>A generic proxy for the FFXIV zone server.</summary>
public interface IZoneProxy : IProxy {
    /// <summary>The public endpoint of the zone proxy.</summary>
    /// <remarks>
    /// This address will be passed to game clients when connecting from a <see cref="ILobbyProxy">lobby proxy</see>.
    /// <b>This address must be reachable by the game clients, not from the perspective of the proxy server.</b>
    /// <list type="bullet">
    /// <item>Proxies running on the local machine can use a <see cref="IPAddress.Loopback">loopback address</see>.</item>
    /// <item>Proxies running on the local network must use a local IP address.</item>
    /// <item>Proxies running on the public Internet must use a public IP address.</item>
    /// </list>
    /// </remarks>
    public IPEndPoint PublicEndpoint { get; }

    /// <summary>Set the server to forward the next connection(s) to.</summary>
    /// <remarks>This will cause a race condition if multiple clients login at once.</remarks>
    public void SetNextServer(IPEndPoint server);
}
