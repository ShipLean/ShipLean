namespace ShipLean.Contracts.Agents;

// An agent connection as an instance admin reads it. The signing secret and the callback token never appear, only their
// last four characters. ArchivedAt is set once the connection is archived: it can no longer be picked or changed.
public sealed record AgentConnectionDto(
    Guid Id,
    string Type,
    string Name,
    string ReceiverUrl,
    string SigningSecretLast4,
    string CallbackTokenLast4,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ArchivedAt);
