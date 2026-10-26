using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Stores;

public sealed class PostgresAgentConsentTests : AgentConsentContract
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

    protected override IAgentConsent ConsentFor(Guid userId) => _world.ConsentFor(userId);

    protected override Guid ArrangeRequest(Guid userId, string clientId, string redirectUri, string? state, IReadOnlyList<string> scopes)
        => _database.InsertRequest(clientId, redirectUri, state, AgentWorld.Challenge, scopes.Order(StringComparer.Ordinal).ToArray(), AgentWorld.Resource, userId, _world.Now + OAuthRequestCreate.Lifetime);
}
