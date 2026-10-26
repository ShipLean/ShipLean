using ShipLean.TestSupport.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// last_used_at is written at most once a minute: a validation inside the minute leaves the row untouched (same xmin).
[TestFixture]
public class PostgresAgentTokensLastUsedTests
{
    private PostgresAgentDatabase _database = null!;
    private PostgresAgentWorld _world = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await PostgresAgentDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [SetUp]
    public void CreateWorld() => _world = _database.NewWorld();

    private Task<string> RowVersionAsync(Guid authorizationId)
        => _database.QueryAsync<string>("SELECT xmin::text || '/' || coalesce(last_used_at::text, '') FROM oauth_authorization WHERE id = @authorizationId", new { authorizationId });

    [Test]
    public async Task Tokens_WhenLastUsedWithinAMinute_ShouldNotWriteAgain()
    {
        var (_, authorizationId, pair) = await _world.IssueAsync();
        (await _world.Tokens.ValidateAsync(pair.AccessToken, AgentWorld.Resource)).ShouldNotBeNull();
        var afterFirst = await RowVersionAsync(authorizationId);

        _world.Advance(TimeSpan.FromSeconds(59));
        (await _world.Tokens.ValidateAsync(pair.AccessToken, AgentWorld.Resource)).ShouldNotBeNull();

        (await RowVersionAsync(authorizationId)).ShouldBe(afterFirst);
    }

    [Test]
    public async Task Tokens_WhenLastUsedAMinuteAgo_ShouldWriteAgain()
    {
        var (_, authorizationId, pair) = await _world.IssueAsync();
        (await _world.Tokens.ValidateAsync(pair.AccessToken, AgentWorld.Resource)).ShouldNotBeNull();
        var afterFirst = await RowVersionAsync(authorizationId);

        _world.Advance(TimeSpan.FromSeconds(60));
        (await _world.Tokens.ValidateAsync(pair.AccessToken, AgentWorld.Resource)).ShouldNotBeNull();

        (await RowVersionAsync(authorizationId)).ShouldNotBe(afterFirst);
    }

    [Test]
    public async Task Validate_WhenClientArchived_ShouldBeNull()
    {
        var (_, authorizationId, pair) = await _world.IssueAsync();
        (await _world.Tokens.ValidateAsync(pair.AccessToken, AgentWorld.Resource)).ShouldNotBeNull();
        await _database.ExecuteAsync(
            "UPDATE oauth_client SET archived_at = @now WHERE client_id = (SELECT client_id FROM oauth_authorization WHERE id = @authorizationId)",
            new { now = _world.Now, authorizationId });

        (await _world.Tokens.ValidateAsync(pair.AccessToken, AgentWorld.Resource)).ShouldBeNull();
        (await _world.RefreshAsync(pair.RefreshToken)).ShouldBeNull();
    }
}
