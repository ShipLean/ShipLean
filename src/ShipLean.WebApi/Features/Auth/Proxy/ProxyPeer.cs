using System.Net;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// The TCP peer that connected to this host, as a request feature. ProxyPeerStartupFilter captures it before
// UseForwardedHeaders runs: for a trusted proxy that middleware rewrites Connection.RemoteIpAddress to the client the
// proxy names, so after it the connection no longer says who connected. Plain-header mode believes an identity header
// only from a trusted raw peer.
public sealed record ProxyPeer(IPAddress? Address)
{
    // The raw peer of this request, or null when none was captured (then nothing is trusted).
    public static IPAddress? Of(HttpRequest request) => request.HttpContext.Features.Get<ProxyPeer>()?.Address;
}
