namespace ShipLean.Contracts.Agents;

// Mints and rotates the token pairs. Access tokens live 1 hour, refresh tokens 90 days from their last use. Secrets are
// 32 random bytes from RandomNumberGenerator, base64url; only their SHA-256 is stored.
public interface IAgentTokenIssuer
{
    // A new pair for an authorization, audience-bound to resource. Throws NotFound for an unknown or revoked authorization, or one whose user is disabled or whose security stamp differs from the user's current one (the rule IAgentTokens.ValidateAsync applies).
    Task<AgentTokenPair> IssueAsync(Guid authorizationId, string resource, CancellationToken cancellationToken = default);

    // Spends the refresh token and returns a new pair in the same family. Null when the refresh token is unknown,
    // expired, revoked, issued to another client than clientId, for another resource, or its authorization is revoked or its user disabled or re-stamped. Reusing
    // a spent refresh token revokes the whole family (and returns null). Concurrent refreshes of one token: one wins, and
    // the reuse detection then revokes the family, including the winner's new pair, so callers must not retry a refresh
    // in parallel.
    Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId, string resource, CancellationToken cancellationToken = default);

    // RFC 7009 revocation by the token itself. An access token revokes just that token; a refresh token revokes its whole
    // family. An unknown, empty or already revoked token, or one issued to another client than clientId (RFC 7009 section 2.1),
    // is a no-op, so a caller can't tell whether it existed.
    Task RevokeAsync(string token, string clientId, CancellationToken cancellationToken = default);
}
