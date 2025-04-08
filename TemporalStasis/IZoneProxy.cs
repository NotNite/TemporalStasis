using System.Net;

namespace TemporalStasis;

/// <summary>A generic proxy for the FFXIV zone server.</summary>
public interface IZoneProxy : IProxy {
    public IPEndPoint PublicEndpoint { get; }

    /// <summary>Set the server to forward the next connection to.</summary>
    /// <remarks>This will cause a race condition if multiple clients login at once.</remarks>
    public void SetNextServer(IPEndPoint server);
}
