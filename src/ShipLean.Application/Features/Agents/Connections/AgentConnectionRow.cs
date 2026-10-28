using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Connections;

// An agent_connection row without its callback token hash; Dapper maps the columns onto the constructor by name.
internal sealed record AgentConnectionRow(
    Guid Id, string Type, string Name, string ReceiverUrl, string SigningSecretProtected, string SigningSecretLast4, string CallbackTokenLast4,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ArchivedAt)
{
    public const string Columns =
        "id, type, name, receiver_url AS receiverUrl, signing_secret_protected AS signingSecretProtected, signing_secret_last4 AS signingSecretLast4, "
        + "callback_token_last4 AS callbackTokenLast4, created_at AS createdAt, updated_at AS updatedAt, archived_at AS archivedAt";

    public AgentConnectionDto ToDto() => new(Id, Type, Name, ReceiverUrl, SigningSecretLast4, CallbackTokenLast4, CreatedAt, UpdatedAt, ArchivedAt);
}
