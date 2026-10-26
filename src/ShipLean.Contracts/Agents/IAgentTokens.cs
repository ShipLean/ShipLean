namespace ShipLean.Contracts.Agents;

// What a bearer token means to /mcp.
public interface IAgentTokens
{
    // Null when the token is unknown, expired or revoked (or its authorization is), the authorization's user is
    // disabled, its client is archived, the user's current security stamp differs from the one stored on the authorization, or resource differs
    // from the token's audience. Checked on every call. Hashes are compared in constant time; the bearer is never logged.
    Task<AgentPrincipal?> ValidateAsync(string bearer, string resource, CancellationToken cancellationToken = default);
}
