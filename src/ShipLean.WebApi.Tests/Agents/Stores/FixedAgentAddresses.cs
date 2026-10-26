using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// The addresses of a request to ship.example.test, for stores that need an issuer.
internal sealed class FixedAgentAddresses : IAgentAddresses
{
    public const string Issuer = "https://ship.example.test";

    public Task<AgentAddresses> ForCurrentRequestAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AgentAddresses(Issuer, Issuer + "/mcp"));
}
