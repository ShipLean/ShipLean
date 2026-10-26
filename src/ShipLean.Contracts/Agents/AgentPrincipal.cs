namespace ShipLean.Contracts.Agents;

// Who a valid agent token acts as: the user who consented, the authorization (one per connected agent), the client as
// consent showed it (Verified: the client's host was verified, so ClientHost can be trusted), and the granted scopes.
public sealed record AgentPrincipal(
    Guid UserId,
    Guid AuthorizationId,
    string ClientId,
    string ClientName,
    string? ClientHost,
    bool Verified,
    IReadOnlySet<string> Scopes);
