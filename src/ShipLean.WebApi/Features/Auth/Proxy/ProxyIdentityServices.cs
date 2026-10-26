using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// What the proxy identity reader needs beyond its own types: the raw-peer capture for plain-header mode, and the HTTP
// client token mode fetches signing keys with. The key URLs are operator configuration (https, or loopback http for a
// local identity provider), so this client deliberately has no SSRF guard: the guard refuses loopback.
public sealed class ProxyIdentityServices : IServiceModule
{
    public const string HttpClientName = "proxy-sign-in-keys";

    public const int MaxKeyDocumentBytes = 1024 * 1024;

    public static readonly TimeSpan KeyRequestTimeout = TimeSpan.FromSeconds(10);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Startup filters wrap each other in registration order, the first outermost. Inserted first, so the peer is
        // captured before anything can rewrite it, the host's own forwarded-headers filter
        // (ASPNETCORE_FORWARDEDHEADERS_ENABLED) included.
        services.Insert(0, ServiceDescriptor.Transient<IStartupFilter, ProxyPeerStartupFilter>());
        // No redirects: each address fetched must be one ProxyKeySource checked. A JWKS is a few kilobytes.
        services.AddHttpClient(HttpClientName, client =>
            {
                client.Timeout = KeyRequestTimeout;
                client.MaxResponseContentBufferSize = MaxKeyDocumentBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
    }
}
