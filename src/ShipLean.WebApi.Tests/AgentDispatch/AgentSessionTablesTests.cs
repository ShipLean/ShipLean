using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.AgentDispatch;

[TestFixture]
public class AgentSessionTablesTests
{
    private FreshDatabase _database = null!;
    private Guid _connection;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _database = await FreshDatabase.CreateAsync();
        _connection = Guid.NewGuid();
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at)
            VALUES (@id, 'linear-compatible', 'Tables relay', 'https://relay.example.com/hook', 'x', 'xxxx', @hash, 'xxxx', now(), now())
            """,
            new { id = _connection, hash = _connection.ToByteArray() });
    }

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    private Task InsertSessionAsync(NpgsqlConnection connection, Guid itemId, string state, Guid? id = null)
        => connection.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at)
            VALUES (@id, @itemId, @connection, @state, 'https://github.com/ShipLean/ShipLean.git', 'main', now(), @joel, now())
            """,
            new { id = id ?? Guid.NewGuid(), itemId, connection = _connection, state, joel = SeedIds.Users.Joel });

    [TestCase("submitted")]
    [TestCase("working")]
    [TestCase("inputRequired")]
    public async Task AgentSession_WhenTheItemHasALiveSession_ShouldRefuseAnotherLiveOne(string live)
    {
        await using var connection = await _database.OpenAsync();
        var item = await new ShipLean.TestSupport.Builders.WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        await InsertSessionAsync(connection, item, live);

        var error = await Should.ThrowAsync<PostgresException>(() => InsertSessionAsync(connection, item, "submitted"));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [TestCase("completed")]
    [TestCase("failed")]
    [TestCase("canceled")]
    [TestCase("stale")]
    public async Task AgentSession_WhenTheOldSessionEnded_ShouldAcceptANewLiveOne(string ended)
    {
        await using var connection = await _database.OpenAsync();
        var item = await new ShipLean.TestSupport.Builders.WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        await InsertSessionAsync(connection, item, ended);

        await InsertSessionAsync(connection, item, "submitted");

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM agent_session WHERE work_item_id = @item", new { item })).ShouldBe(2);
    }

    [Test]
    public async Task AgentSession_WhenStateIsUnknown_ShouldBeRefused()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new ShipLean.TestSupport.Builders.WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => InsertSessionAsync(connection, item, "running"));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task AgentDelivery_WhenTheSequenceRepeatsInASession_ShouldBeRefused()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new ShipLean.TestSupport.Builders.WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var session = Guid.NewGuid();
        await InsertSessionAsync(connection, item, "submitted", session);
        const string insert =
            "INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at) VALUES (gen_random_uuid(), @session, @seq, @action, '{}'::jsonb, now(), 'pending', now())";
        await connection.ExecuteAsync(insert, new { session, seq = 1, action = "created" });
        await connection.ExecuteAsync(insert, new { session, seq = 2, action = "prompted" });

        var error = await Should.ThrowAsync<PostgresException>(() => connection.ExecuteAsync(insert, new { session, seq = 2, action = "prompted" }));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }
}
