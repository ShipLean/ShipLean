using ShipLean.TestSupport.Contracts.Agents;

namespace ShipLean.UnitTest.Contracts.Agents;

public sealed class InMemoryAgentTokenIssuerTests : AgentTokenIssuerContract
{
    private InMemoryAgentWorld _world = null!;

    [SetUp]
    public void CreateWorld() => _world = new InMemoryAgentWorld();

    protected override AgentWorld World => _world;
}
