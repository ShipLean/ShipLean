namespace ShipLean.Contracts.Agents;

public interface IOAuthClients
{
    // By client_id (a metadata-document URL or a registered id). Null when unknown or archived.
    Task<OAuthClient?> FindAsync(string clientId, CancellationToken cancellationToken = default);

    // Dynamic client registration. Throws Validation for a blank name, no redirect URI, or a redirect that is neither
    // http on a loopback host nor https, or that carries userinfo.
    Task<OAuthClient> RegisterAsync(OAuthClientRegistration request, CancellationToken cancellationToken = default);
}
