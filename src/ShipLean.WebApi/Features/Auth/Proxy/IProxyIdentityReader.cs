namespace ShipLean.WebApi.Features.Auth.Proxy;

// Who the identity proxy says this request's user is. It takes the HttpRequest, not the HttpContext (AG0003).
// Plain-header mode trusts the raw TCP peer, captured before UseForwardedHeaders runs (ProxyPeerStartupFilter), never
// Connection.RemoteIpAddress: after forwarding that is the end client the proxy names, not the proxy itself.
// Rules for every implementation:
// - Null means no identity: none sent, verification failed (signature, issuer, lifetime, audience, a missing or
//   rejected email), or plain-header mode from an untrusted peer.
// - A bad token never throws. The reader logs the reason at Warning, with the claim or header names, and never the
//   token, a cookie or a whole header value.
// - A returned identity is clean: Email went through IdentityInput.Email, and Name through IdentityInput.Name (null
//   when missing, blank or rejected).
public interface IProxyIdentityReader
{
    Task<ProxyIdentity?> ReadAsync(HttpRequest request, CancellationToken ct);
}
