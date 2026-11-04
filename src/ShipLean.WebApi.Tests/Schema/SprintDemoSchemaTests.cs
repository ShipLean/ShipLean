using Dapper;
using Npgsql;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Schema;

// K3: the sprint's demo fields, the per-project demo settings and YouTube channel, and the transfer token's new kinds.
[TestFixture]
public class SprintDemoSchemaTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Sprint_WhenDemoItemUsedTwice_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);
        var first = await InsertSprintAsync(connection);
        var second = await InsertSprintAsync(connection);
        await connection.ExecuteAsync("UPDATE sprint SET demo_work_item_id = @item WHERE id = @first", new { item, first });

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE sprint SET demo_work_item_id = @item WHERE id = @second", new { item, second }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Test]
    public async Task Sprint_WhenDemoYouTubeStateUnknown_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var sprint = await InsertSprintAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE sprint SET demo_youtube_state = 'uploading' WHERE id = @sprint", new { sprint }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task Sprint_WhenDemoRequestedViaUnknown_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var sprint = await InsertSprintAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () =>
            await connection.ExecuteAsync("UPDATE sprint SET demo_requested_via = 'email' WHERE id = @sprint", new { sprint }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task Sprint_WhenCreatedWithNoDemo_ShouldHaveStateNone()
    {
        await using var connection = await _database.OpenAsync();
        var sprint = await InsertSprintAsync(connection);

        var row = await connection.QuerySingleAsync<(string State, bool NoItem, bool NoRequest)>(
            "SELECT demo_youtube_state, demo_work_item_id IS NULL, demo_requested_at IS NULL FROM sprint WHERE id = @sprint", new { sprint });

        row.ShouldBe(("none", true, true));
    }

    [Test]
    public async Task YouTubeChannel_WhenSecondLiveForProject_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var project = SeedIds.Projects.Pay;
        await InsertChannelAsync(connection, project, archived: false);
        await InsertChannelAsync(connection, project, archived: true);

        var ex = await Should.ThrowAsync<PostgresException>(async () => await InsertChannelAsync(connection, project, archived: false));

        ex.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [TestCase("demo")]
    [TestCase("demoLogo")]
    public async Task TransferToken_WhenKindDemoOrDemoLogo_ShouldInsert(string kind)
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);

        await connection.ExecuteAsync(
            """
            INSERT INTO transfer_token (token_hash, kind, work_item_id, name, content_type, user_id, agent_stamp, expires_at)
            VALUES (sha256(gen_random_uuid()::text::bytea), @kind, @item, 'n', 'video/mp4', @user, 'ShipLean', now() + interval '1 day')
            """, new { kind, item, user = SeedIds.Users.Joel });

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM transfer_token WHERE work_item_id = @item AND kind = @kind AND revoked_at IS NULL", new { item, kind })).ShouldBe(1);
    }

    [Test]
    public async Task TransferToken_WhenKindUnknown_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(connection);

        var ex = await Should.ThrowAsync<PostgresException>(async () => await connection.ExecuteAsync(
            """
            INSERT INTO transfer_token (token_hash, kind, work_item_id, name, content_type, user_id, agent_stamp, expires_at)
            VALUES (sha256(gen_random_uuid()::text::bytea), 'logo', @item, 'n', 'video/mp4', @user, 'ShipLean', now() + interval '1 day')
            """, new { item, user = SeedIds.Users.Joel }));

        ex.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task DemoSettings_WhenNoLogo_ShouldAllowNull()
    {
        await using var connection = await _database.OpenAsync();

        await connection.ExecuteAsync("INSERT INTO project_demo_settings (project_id) VALUES (@project)", new { project = SeedIds.Projects.Mob });

        var row = await connection.QuerySingleAsync<(bool Automatic, string Environment, bool NoLogo, int Version, string Tokens)>(
            "SELECT automatic, environment, logo_blob IS NULL, logo_version, tokens FROM project_demo_settings WHERE project_id = @project", new { project = SeedIds.Projects.Mob });
        row.ShouldBe((false, "", true, 0, ""));
    }

    private static async Task<Guid> InsertSprintAsync(NpgsqlConnection connection)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, @name, DATE '2031-01-01', DATE '2031-01-14')",
            new { id, project = SeedIds.Projects.Ship, name = $"Demo schema {id:N}" });
        return id;
    }

    private static Task InsertChannelAsync(NpgsqlConnection connection, Guid project, bool archived)
        => connection.ExecuteAsync(
            """
            INSERT INTO project_youtube_channel (id, project_id, status, channel_id, channel_title, refresh_token, connected_by, archived_at)
            VALUES (gen_random_uuid(), @project, 'pending', 'UC1', 'Channel', 'tok', @user, CASE WHEN @archived THEN now() END)
            """, new { project, user = SeedIds.Users.Joel, archived });
}
