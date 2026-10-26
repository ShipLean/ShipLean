using ShipLean.Contracts.Agents;

namespace ShipLean.UnitTest.Contracts.Agents;

// The addresses of a request to ship.example.test, for fakes that need an issuer.
internal sealed class FixedAgentAddresses : IAgentAddresses
{
    public const string Issuer = "https://ship.example.test";

    public Task<AgentAddresses> ForCurrentRequestAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AgentAddresses(Issuer, Issuer + "/mcp"));
}
