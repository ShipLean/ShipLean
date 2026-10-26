using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// The metadata-document half of IOAuthClients.FindAsync: fetched through IClientMetadataDocuments, cached in oauth_client.
[TestFixture]
public class PostgresOAuthClientsMetadataTests
{
    private PostgresAgentDatabase _database = null!;
    private PostgresAgentWorld _world = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await PostgresAgentDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [SetUp]
    public void CreateWorld() => _world = _database.NewWorld();

    private static string KnownId => StubClientMetadataDocuments.DocumentUrl;

    [Test]
    public async Task Find_WhenMetadataDocumentClient_ShouldReturnVerifiedClientAndCacheIt()
    {
        var client = await _world.Clients.FindAsync(KnownId);

        client.ShouldNotBeNull();
        client.Kind.ShouldBe(OAuthClientKind.MetadataDocument);
        client.Verified.ShouldBeTrue();
        client.ClientHost.ShouldBe("agent.example.test");
        var cached = await _database.QueryAsync<string>("SELECT client_host FROM oauth_client WHERE client_id = @id AND kind = 'metadataDocument' AND metadata_expires_at > @now", new { id = KnownId, now = _world.Now });
        cached.ShouldBe("agent.example.test");
    }

    [Test]
    public async Task Find_WhenCachedAndNotExpired_ShouldNotFetchAgain()
    {
        await _world.Clients.FindAsync(KnownId);
        await _database.ExecuteAsync("UPDATE oauth_client SET client_name = 'Cached name' WHERE client_id = @id", new { id = KnownId });

        (await _world.Clients.FindAsync(KnownId))!.ClientName.ShouldBe("Cached name");
    }

    [Test]
    public async Task Find_WhenCacheExpired_ShouldFetchAgain()
    {
        await _world.Clients.FindAsync(KnownId);
        await _database.ExecuteAsync("UPDATE oauth_client SET client_name = 'Stale name' WHERE client_id = @id", new { id = KnownId });
        _world.Advance(StubClientMetadataDocuments.Lifetime + TimeSpan.FromSeconds(1));

        (await _world.Clients.FindAsync(KnownId))!.ClientName.ShouldBe(StubClientMetadataDocuments.Name);
    }

    [Test]
    public async Task Find_WhenHttpsClientIdIsNotADocument_ShouldReturnNull()
    {
        (await _world.Clients.FindAsync("https://nobody.example.test/client.json")).ShouldBeNull();
    }
}
