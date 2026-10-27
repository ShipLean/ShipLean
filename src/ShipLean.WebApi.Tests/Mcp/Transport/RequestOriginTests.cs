using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The host a published URL uses. The dialed host is the in-cluster service; the host the request came in on is the
// last X-Forwarded-Host from a trusted peer.
[TestFixture]
public class RequestOriginTests
{
    [Test]
    public void Of_WhenTrustedProxyNamesTheHost_ShouldUseThatHostAndScheme()
    {
        var request = Request("http://non-prod.shiplean.svc:8080", "127.0.0.1");
        request.Headers["X-Forwarded-Host"] = "shiplean.agoda.is";
        request.Headers["X-Forwarded-Proto"] = "https";

        RequestOrigin.Of(request, new ForwardedHeadersOptions()).ShouldBe("https://shiplean.agoda.is");
    }

    [Test]
    public void Of_WhenHeaderListsSeveralHosts_ShouldUseTheLast()
    {
        var request = Request("http://non-prod.shiplean.svc:8080", "127.0.0.1");
        request.Headers["X-Forwarded-Host"] = "evil.example, shiplean.agoda.is";
        request.Headers["X-Forwarded-Proto"] = "http, https";

        RequestOrigin.Of(request, new ForwardedHeadersOptions()).ShouldBe("https://shiplean.agoda.is");
    }

    [Test]
    public void Of_WhenForwardedHostIsTheClusterService_ShouldKeepTheDialedHost()
    {
        var request = Request("http://non-prod.shiplean.svc:8080", "127.0.0.1");
        request.Headers["X-Forwarded-Host"] = "non-prod.shiplean.svc.cluster.local";
        request.Headers["X-Forwarded-Proto"] = "https";

        RequestOrigin.Of(request, new ForwardedHeadersOptions()).ShouldBe("http://non-prod.shiplean.svc:8080");
    }

    [Test]
    public void Of_WhenPeerIsNotTrusted_ShouldIgnoreTheForwardedHost()
    {
        var request = Request("http://127.0.0.1:8080", "203.0.113.8");
        request.Headers["X-Forwarded-Host"] = "evil.example";
        request.Headers["X-Forwarded-Proto"] = "https";

        RequestOrigin.Of(request, new ForwardedHeadersOptions()).ShouldBe("http://127.0.0.1:8080");
    }

    [Test]
    public void Of_WhenNoForwardedHost_ShouldUseTheDialedHost()
    {
        var request = Request("http://127.0.0.1:8080", "127.0.0.1");

        RequestOrigin.Of(request, new ForwardedHeadersOptions()).ShouldBe("http://127.0.0.1:8080");
    }

    private static HttpRequest Request(string dialed, string peer)
    {
        var context = new DefaultHttpContext();
        var uri = new Uri(dialed);
        context.Request.Scheme = uri.Scheme;
        context.Request.Host = HostString.FromUriComponent(uri.IsDefaultPort ? uri.Host : uri.Authority);
        context.Features.Set(new ProxyPeer(IPAddress.Parse(peer)));
        return context.Request;
    }
}
