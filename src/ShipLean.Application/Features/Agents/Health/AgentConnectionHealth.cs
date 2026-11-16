using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Health;

// An agent connection's recent delivery health, read from agent_delivery_attempt.
public sealed class AgentConnectionHealth : IAgentConnectionHealth
{
    // TODO: empty health until the read over the connection's delivery attempts replaces it.
    public Task<AgentConnectionHealthDto> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
        => Task.FromResult(new AgentConnectionHealthDto(null, null, 0, null, null, null));
}
