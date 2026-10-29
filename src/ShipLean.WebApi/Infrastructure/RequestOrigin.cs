using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;

namespace ShipLean.WebApi.Infrastructure;

// The origin to publish in a URL a caller opens from outside the cluster. request.Host is the address the last hop
// dialed. The MCP gateway dials the service DNS, so that host does not resolve on a laptop. A trusted proxy names the
// host the request came in on in X-Forwarded-Host, and the scheme in X-Forwarded-Proto. Those headers are read here
// only, and only the last value (the nearest trusted hop, the same limit as the other forwarded headers). They are not
// applied to Request.Host: a redirect still goes to the app's own host. A cluster service name (*.svc) is the mesh
// dial address, not the host the request came in on, so it is ignored. When the origin that remains is still that
// name, ClusterFallback says why, and PublicOrigin logs it once.
public static class RequestOrigin
{
    public static string Of(HttpRequest request, ForwardedHeadersOptions forwarding)
    {
        if (Trusted(request, forwarding) && TryForwarded(request, out var forwarded))
        {
            return forwarded;
        }
        return $"{request.Scheme}://{request.Host.ToUriComponent()}";
    }

    // Why Of fell back to a cluster service name, or null when the origin is one a laptop can open. The peer wins when
    // it is not trusted, because a correct X-Forwarded-Host would still be ignored.
    public static string? ClusterFallback(HttpRequest request, ForwardedHeadersOptions forwarding)
    {
        if (!HostIsClusterService(Of(request, forwarding)))
        {
            return null;
        }
        if (!Trusted(request, forwarding))
        {
            var peer = ProxyPeer.Of(request);
            return $"peer {peer?.ToString() ?? "unknown"} is not in SHIPLEAN_TRUSTED_PROXIES";
        }
        var host = Last(request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName]);
        if (host is null)
        {
            return "no X-Forwarded-Host";
        }
        if (IsClusterService(host))
        {
            return $"X-Forwarded-Host {host} is a cluster service name";
        }
        return "X-Forwarded-Host was not a usable public host";
    }

    // True for an origin or host whose name is a cluster service (*.svc or *.svc.cluster.local).
    public static bool HostIsClusterService(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.IdnHost))
        {
            return IsClusterService(uri.IdnHost);
        }
        return IsClusterService(origin);
    }

    private static bool Trusted(HttpRequest request, ForwardedHeadersOptions forwarding)
    {
        var peer = ProxyPeer.Of(request);
        return peer is not null && (forwarding.KnownProxies.Contains(peer) || forwarding.KnownIPNetworks.Any(network => network.Contains(peer)));
    }

    private static bool TryForwarded(HttpRequest request, out string origin)
    {
        origin = string.Empty;
        var host = Last(request.Headers[ForwardedHeadersDefaults.XForwardedHostHeaderName]);
        if (host is null || IsClusterService(host))
        {
            return false;
        }
        var proto = Last(request.Headers[ForwardedHeadersDefaults.XForwardedProtoHeaderName]);
        var scheme = proto is "http" or "https" ? proto : request.Scheme;
        if (scheme is not ("http" or "https") || !Uri.TryCreate($"{scheme}://{host}", UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }
        if (IsClusterService(uri.IdnHost))
        {
            return false;
        }
        origin = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static string? Last(StringValues values)
    {
        var parts = values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts[^1];
    }

    private static bool IsClusterService(string host)
    {
        var name = HostString.FromUriComponent(host).Host;
        return name.EndsWith(".svc", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".svc.cluster.local", StringComparison.OrdinalIgnoreCase);
    }
}
