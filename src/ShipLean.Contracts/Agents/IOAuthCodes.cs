namespace ShipLean.Contracts.Agents;

// Authorization codes: S256 PKCE only, single use, 60 seconds.
public interface IOAuthCodes
{
    // A new code for an authorization. The store keeps its hash, the challenge, the redirect URI and the resource.
    Task<string> IssueAsync(Guid authorizationId, string codeChallenge, string redirectUri, string resource, CancellationToken cancellationToken = default);

    // The authorization id, or null when the code is unknown, expired, or any argument differs from what was issued
    // (BASE64URL(SHA256(codeVerifier)) must equal the challenge; clientId must be the authorization's client). Redeeming
    // a code that was already used returns null and revokes that authorization (RFC 6749 section 4.1.2).
    Task<Guid?> RedeemAsync(string code, string codeVerifier, string redirectUri, string clientId, string resource, CancellationToken cancellationToken = default);
}
