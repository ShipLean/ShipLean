using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Contracts.Agents;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Schema;

// C1: the session's end columns and their back-fill, the delivery attempt table, and the transfer token's agentLog kind and session.
[TestFixture]
public class AgentSessionTroubleshootingSchemaTests
{
    private const string Migration = "db/migrations/202611151200_agents_session_troubleshooting.sql";

    private FreshDatabase _database = null!;
    private Guid _connection;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _database = await FreshDatabase.CreateAsync();
        await using var connection = await _database.OpenAsync();
        _connection = await AgentSessionSeeds.InsertConnectionAsync(connection);
    }

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    // The migration's own back-fill statement, as the file holds it, run again over sessions arranged as they were before it.
    private static string BackFill()
        => DatabaseScripts.Load(includeSeed: false).Single(s => s.Name == Migration).Contents
            .Split('\n').Single(line => line.StartsWith("UPDATE agent_session SET end_reason", StringComparison.Ordinal));

    private async Task<Guid> NewSessionAsync(NpgsqlConnection connection, string state)
    {
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        return await new AgentSessionSeeds(item, _connection).WithState(state).InsertAsync(connection);
    }

    [Test]
    public async Task BackFill_WhenRun_ShouldMarkEndedSessionsUnknownAndLeaveLiveOnesNull()
    {
        await using var connection = await _database.OpenAsync();
        string[] states = ["submitted", "working", "inputRequired", "completed", "failed", "canceled", "stale"];
        var sessions = new Dictionary<string, Guid>();
        foreach (var state in states)
        {
            sessions[state] = await NewSessionAsync(connection, state);
        }

        await connection.ExecuteAsync(BackFill());

        var reasons = (await connection.QueryAsync<(Guid Id, string? Reason)>(
            "SELECT id, end_reason FROM agent_session WHERE id = ANY(@ids)", new { ids = sessions.Values.ToArray() })).ToDictionary(r => r.Id, r => r.Reason);
        states.Select(state => (state, reasons[sessions[state]])).ShouldBe(
        [
            ("submitted", null), ("working", null), ("inputRequired", null),
            ("completed", AgentEndReasons.Unknown), ("failed", AgentEndReasons.Unknown), ("canceled", AgentEndReasons.Unknown), ("stale", AgentEndReasons.Unknown),
        ]);
    }

    [Test]
    public async Task EndReason_WhenEveryContractValue_ShouldBeAccepted()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);

        foreach (var reason in AgentEndReasons.All)
        {
            await new AgentSessionSeeds(item, _connection).WithState("failed").WithEndReason(reason).InsertAsync(connection);
        }

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM agent_session WHERE work_item_id = @item", new { item })).ShouldBe(AgentEndReasons.All.Count);
    }

    [Test]
    public async Task EndReason_WhenUnknownValue_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(() => new AgentSessionSeeds(item, _connection).WithState("failed").WithEndReason("connectionGone").InsertAsync(connection));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task EndDetail_WhenLongerThanOneThousand_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        await new AgentSessionSeeds(item, _connection).WithState("failed").WithEndReason(AgentEndReasons.AgentError).WithEndDetail(new string('x', 1000)).InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(() => new AgentSessionSeeds(item, _connection)
            .WithState("failed").WithEndReason(AgentEndReasons.AgentError).WithEndDetail(new string('x', 1001)).InsertAsync(connection));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task DeliveryAttempt_WhenTheAttemptNumberRepeats_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var session = await NewSessionAsync(connection, "failed");
        var delivery = await AgentDeliveryAttemptRows.InsertDeliveryAsync(connection, session);
        var attempt = new AgentDeliveryAttemptCreate(delivery, _connection, 1, TestDates.FarFuture, 502, "Bad gateway.", 40, null);
        await AgentDeliveryAttemptRows.InsertAsync(connection, attempt);

        var ex = await Should.ThrowAsync<PostgresException>(() => AgentDeliveryAttemptRows.InsertAsync(connection, attempt));

        ex.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Test]
    public async Task DeliveryAttempt_WhenErrorLongerThanOneThousand_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var session = await NewSessionAsync(connection, "failed");
        var delivery = await AgentDeliveryAttemptRows.InsertDeliveryAsync(connection, session);

        var ex = await Should.ThrowAsync<PostgresException>(() => AgentDeliveryAttemptRows.InsertAsync(
            connection, new AgentDeliveryAttemptCreate(delivery, _connection, 1, TestDates.FarFuture, null, new string('x', 1001), 40, null)));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [TestCase("upload")]
    [TestCase("download")]
    [TestCase("demo")]
    [TestCase("demoLogo")]
    [TestCase("agentLog")]
    public async Task TransferToken_WhenAnyCurrentKind_ShouldInsert(string kind)
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        Guid? session = kind == "agentLog" ? await new AgentSessionSeeds(item, _connection).WithState("working").InsertAsync(connection) : null;

        await connection.ExecuteAsync(
            """
            INSERT INTO transfer_token (token_hash, kind, work_item_id, name, content_type, user_id, agent_stamp, expires_at, session_id)
            VALUES (sha256(gen_random_uuid()::text::bytea), @kind, @item, 'n', 'text/plain', @user, 'ShipLean', now() + interval '1 day', @session)
            """, new { kind, item, user = SeedIds.Users.Joel, session });

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM transfer_token WHERE work_item_id = @item AND kind = @kind", new { item, kind })).ShouldBe(1);
    }
}
