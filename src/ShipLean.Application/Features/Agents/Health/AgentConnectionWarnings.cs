using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Health;

// The Start agent and Create demo warning for a connection whose newest delivery attempt failed.
public sealed class AgentConnectionWarnings : IAgentConnectionWarnings
{
    // TODO: no warning until the read over the connection's newest failed and successful attempts replaces it.
    public Task<string?> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
