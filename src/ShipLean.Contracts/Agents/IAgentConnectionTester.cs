namespace ShipLean.Contracts.Agents;

// Test connection (POST api/instance/agent-connections/{id}/test): sends the connection a signed ping with no item data. Not
// stored as a delivery attempt. An archived connection is a validation error.
public interface IAgentConnectionTester
{
    Task<AgentConnectionTestDto> TestAsync(Guid connectionId, CancellationToken cancellationToken);
}
