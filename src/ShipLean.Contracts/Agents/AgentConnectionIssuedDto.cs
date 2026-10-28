namespace ShipLean.Contracts.Agents;

// What creating a connection or regenerating one of its keys answers. SigningSecret and CallbackToken are the only
// time the plaintext leaves the server: each is set when it was just made and null otherwise.
public sealed record AgentConnectionIssuedDto(AgentConnectionDto Connection, string? SigningSecret, string? CallbackToken)
{
    // The record's own ToString would print the plaintext secret and token into any log line.
    public override string ToString() => $"AgentConnectionIssuedDto {{ Id = {Connection?.Id} }}";
}
