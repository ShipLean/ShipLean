using ShipLean.TestSupport.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Stores;

public sealed class PostgresOAuthClientsTests : OAuthClientsContract
{
    private PostgresAgentDatabase _database = null!;
    private PostgresAgentWorld _world = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await PostgresAgentDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [SetUp]
    public void CreateWorld() => _world = _database.NewWorld();

    protected override AgentWorld World => _world;
}
