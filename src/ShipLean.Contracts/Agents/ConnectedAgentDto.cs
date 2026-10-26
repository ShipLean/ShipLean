namespace ShipLean.Contracts.Agents;

// One row of "Connected agents": an authorization that is not revoked.
public sealed record ConnectedAgentDto(
    Guid Id,
    string ClientName,
    string? ClientHost,
    bool Verified,
    IReadOnlyList<string> Scopes,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastUsedAt,
    string ConnectedLabel,
    string? LastUsedLabel);
