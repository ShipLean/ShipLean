namespace ShipLean.Contracts.Agents;

// An access token (1 hour) and its refresh token (90 days, sliding). The secrets exist only here: the store keeps hashes.
public sealed record AgentTokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt, IReadOnlySet<string> Scopes);
