using Dapper;
using Npgsql;
using Shouldly;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Agents;

[TestFixture]
public class AgentTablesTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Migration_WhenApplied_ShouldCreateTheFiveOAuthTables()
    {
        await using var connection = await _database.OpenAsync();

        var tables = await connection.QueryAsync<string>(
            "SELECT table_name::text FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'oauth\\_%' ORDER BY 1");

        tables.ToArray().ShouldBe(["oauth_authorization", "oauth_client", "oauth_code", "oauth_request", "oauth_token"]);
    }

    [TestCase("oauth_client", "client_id")]
    [TestCase("oauth_code", "code_hash")]
    [TestCase("oauth_token", "token_hash")]
    public async Task Migration_WhenApplied_ShouldHaveUniqueLookupColumn(string table, string column)
    {
        await using var connection = await _database.OpenAsync();

        var unique = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
              SELECT 1 FROM pg_index i
              JOIN pg_class c ON c.oid = i.indrelid
              JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = i.indkey[0]
              WHERE c.relname = @table AND i.indisunique AND i.indnatts = 1 AND a.attname = @column)
            """,
            new { table, column });

        unique.ShouldBeTrue();
    }

    [TestCase("oauth_code", "code_hash")]
    [TestCase("oauth_token", "token_hash")]
    public async Task Migration_WhenApplied_ShouldStoreSecretsAsBytes(string table, string column)
    {
        await using var connection = await _database.OpenAsync();

        var type = await connection.ExecuteScalarAsync<string>(
            "SELECT data_type::text FROM information_schema.columns WHERE table_name = @table AND column_name = @column",
            new { table, column });

        type.ShouldBe("bytea");
    }

    [Test]
    public async Task Migration_WhenClientIdRepeated_ShouldRefuseTheSecondRow()
    {
        await using var connection = await _database.OpenAsync();
        var clientId = await new OAuthClientBuilder().InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => new OAuthClientBuilder().WithClientId(clientId).InsertAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Test]
    public async Task OAuthClientBuilder_WhenMetadataDocument_ShouldInsertVerifiedClient()
    {
        await using var connection = await _database.OpenAsync();

        var clientId = await new OAuthClientBuilder().WithMetadataDocument("claude.ai", TimeProvider.System.GetUtcNow().AddDays(1)).InsertAsync(connection);

        var kind = await connection.ExecuteScalarAsync<string>("SELECT kind FROM oauth_client WHERE client_id = @clientId", new { clientId });
        kind.ShouldBe("metadataDocument");
    }

    [Test]
    public async Task Migration_WhenApplied_ShouldLimitLiveAuthorizationsToOnePerUserAndClient()
    {
        await using var connection = await _database.OpenAsync();

        var definition = await connection.ExecuteScalarAsync<string>(
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'oauth_authorization' AND indexname = 'ux_oauth_authorization_live_user_client'");

        definition.ShouldNotBeNull();
        definition.ShouldContain("UNIQUE");
        definition.ShouldContain("(user_id, client_id)");
        definition.ShouldContain("revoked_at IS NULL");
    }

    [Test]
    public async Task Migration_WhenApplied_ShouldLetARequestBeMarkedDecided()
    {
        await using var connection = await _database.OpenAsync();

        var nullable = await connection.ExecuteScalarAsync<string>(
            "SELECT is_nullable::text FROM information_schema.columns WHERE table_name = 'oauth_request' AND column_name = 'decided_at'");

        nullable.ShouldBe("YES");
    }
}
