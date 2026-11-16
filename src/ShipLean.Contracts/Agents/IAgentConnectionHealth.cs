namespace ShipLean.Contracts.Agents;

// An agent connection's recent delivery health, from its delivery attempts (GET api/instance/agent-connections/{id}/health).
public interface IAgentConnectionHealth
{
    Task<AgentConnectionHealthDto> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken);
}
