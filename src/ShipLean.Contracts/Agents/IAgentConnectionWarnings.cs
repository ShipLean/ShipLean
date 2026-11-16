namespace ShipLean.Contracts.Agents;

// The warning shown by Start agent and Create demo: "The last delivery to this connection failed <relative time>." when the
// connection's newest attempt failed and is newer than its newest success; otherwise null.
public interface IAgentConnectionWarnings
{
    Task<string?> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken);
}
