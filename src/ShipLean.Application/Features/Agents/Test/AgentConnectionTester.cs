using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Test;

// Test connection: a signed ping with no item data, sent with the deliverer's signing and named HttpClient.
public sealed class AgentConnectionTester : IAgentConnectionTester
{
    // TODO: reports not reached until the signed ping (any HTTP answer counts as reached) replaces it.
    public Task<AgentConnectionTestDto> TestAsync(Guid connectionId, CancellationToken cancellationToken)
        => Task.FromResult(new AgentConnectionTestDto(false, null, "Test connection is not built yet.", 0));
}
