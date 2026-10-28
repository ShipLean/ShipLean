namespace ShipLean.Contracts.Agents;

// An agent connection as the dispatch code reads it: with the signing secret decrypted, because ShipLean signs the webhook
// with it. Never serialised to a client. The callback token is not here: only its hash is stored.
public sealed record AgentConnection(Guid Id, string Type, string Name, string ReceiverUrl, string SigningSecret, DateTimeOffset? ArchivedAt)
{
    public override string ToString() => $"AgentConnection {{ Id = {Id}, Name = {Name} }}";
}
