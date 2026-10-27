using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// What one request calls the server: the issuer and the /mcp resource every token is bound to, both canonicalised by
// AgentUrls, and whether instance_settings.base_url was set (it is what makes an Origin verifiable).
public sealed record AgentAddresses(string Issuer, string Resource, bool BaseUrlSet)
{
    public string ResourceMetadataUrl => Issuer + AgentUrls.ProtectedResourceMetadataPath + AgentUrls.McpPath;

    // Whether an Origin header names this server: the same scheme, host and port as the issuer. "null" and anything that is
    // not an absolute http(s) origin is foreign. With no base_url the issuer is the host the request came in on, which a
    // direct caller controls, so no browser Origin can be verified and every one is foreign.
    public bool IsOwnOrigin(string origin)
        => BaseUrlSet
           && Uri.TryCreate(origin, UriKind.Absolute, out var given)
           && Uri.TryCreate(Issuer, UriKind.Absolute, out var own)
           && (given.Scheme == Uri.UriSchemeHttp || given.Scheme == Uri.UriSchemeHttps)
           && string.Equals(given.Scheme, own.Scheme, StringComparison.OrdinalIgnoreCase)
           && string.Equals(given.Host, own.Host, StringComparison.OrdinalIgnoreCase)
           && given.Port == own.Port;
}
