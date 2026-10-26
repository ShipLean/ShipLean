using ShipLean.Contracts.Agents;

namespace ShipLean.AcceptanceTests.Lanes.Agent.Screens;

// The real IAgentAddresses only answers inside a request; a test arranging a pending request outside one pins this.
internal sealed class FixedAgentAddresses : IAgentAddresses
{
    public Task<AgentAddresses> ForCurrentRequestAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentAddresses("https://shiplean.larkspur.dev", "https://shiplean.larkspur.dev/mcp"));
}
