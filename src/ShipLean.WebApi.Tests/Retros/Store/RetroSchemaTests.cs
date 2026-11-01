using Dapper;
using Npgsql;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Retros.Store;

// The retro tables' constraints (C2), on a fresh seeded database. Each test uses its own rows so none depends on another.
[TestFixture]
public class RetroSchemaTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Insert_WhenRootCauseOnNonBadPoint_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var retro = await InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "root-cause-agent");

        var error = await Should.ThrowAsync<PostgresException>(() => InsertPointAsync(connection, retro, "improve", "Wait on a locator.", "Because."));

        error.ConstraintName.ShouldBe("ck_retro_point_root_cause");
        await InsertPointAsync(connection, retro, "wentBadly", "A bad thing.", "Because.");
    }

    [Test]
    public async Task Insert_WhenNeitherItemNorSprint_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();

        var error = await Should.ThrowAsync<PostgresException>(() => InsertRetroAsync(connection, null, null, "neither-agent"));

        error.ConstraintName.ShouldBe("ck_retrospective_target");
        await InsertRetroAsync(connection, null, SeedIds.Sprints.Sprint24, "neither-agent");
    }

    [Test]
    public async Task Insert_WhenSecondLiveRetroSameAgentSameItem_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var first = await InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "append-agent");

        var error = await Should.ThrowAsync<PostgresException>(() => InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "append-agent"));

        error.ConstraintName.ShouldBe("ux_retrospective_item_agent");
        await connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @first", new { first });
        await InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "append-agent");
    }

    [Test]
    public async Task Insert_WhenSecondLiveThemeSameNameDifferentCase_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        await InsertThemeAsync(connection, "Flaky Waits");

        var error = await Should.ThrowAsync<PostgresException>(() => InsertThemeAsync(connection, "flaky waits"));

        error.ConstraintName.ShouldBe("ux_retro_theme_name");
    }

    [Test]
    public async Task Insert_WhenUnknownCategory_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var retro = await InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "category-agent");

        var error = await Should.ThrowAsync<PostgresException>(() => InsertPointAsync(connection, retro, "wentSideways", "Nope.", null));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
    }

    [Test]
    public async Task Search_WhenTsvQueried_ShouldMatchRootCauseAndLogText()
    {
        await using var connection = await _database.OpenAsync();
        var retro = await InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "search-agent");
        var point = await InsertPointAsync(connection, retro, "wentBadly", "Deploys stalled.", "Quokkas chewed the cabling.");
        var log = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO retro_failure_log (id, retrospective_id, kind, text) VALUES (@log, @retro, 'friction', 'Zebrafish timeouts on the runner.')",
            new { log, retro });

        var byRootCause = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM retro_point WHERE tsv @@ plainto_tsquery('english', 'quokka cable')");
        var byLog = await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM retro_failure_log WHERE tsv @@ plainto_tsquery('english', 'zebrafish timeout')");

        byRootCause.ShouldBe(point);
        byLog.ShouldBe(log);
    }

    [Test]
    public async Task RetroAction_WhenSecondActionForSamePoint_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var retro = await InsertRetroAsync(connection, SeedIds.WorkItems.Ship90, null, "action-agent");
        var point = await InsertPointAsync(connection, retro, "improve", "Add a retry.", null);
        await InsertActionAsync(connection, SeedIds.WorkItems.Ship91, point);

        var error = await Should.ThrowAsync<PostgresException>(() => InsertActionAsync(connection, SeedIds.WorkItems.Ship92, point));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_retro_action_point");
    }

    private static Task InsertActionAsync(NpgsqlConnection connection, Guid workItem, Guid point)
        => connection.ExecuteAsync(
            "INSERT INTO retro_action (work_item_id, project_id, point_id) VALUES (@workItem, @project, @point)",
            new { workItem, project = SeedIds.Projects.Ship, point });

    private static async Task<Guid> InsertRetroAsync(NpgsqlConnection connection, Guid? workItem, Guid? sprint, string agent)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retrospective (id, project_id, work_item_id, sprint_id, author_user_id, agent_name, role)
            VALUES (@id, @project, @workItem, @sprint, @user, @agent, 'subAgent')
            """,
            new { id, project = SeedIds.Projects.Ship, workItem, sprint, user = SeedIds.Users.Joel, agent });
        return id;
    }

    private static async Task<Guid> InsertPointAsync(NpgsqlConnection connection, Guid retro, string category, string text, string? rootCause)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_point (id, retrospective_id, category, text, root_cause, created_by_user_id)
            VALUES (@id, @retro, @category, @text, @rootCause, @user)
            """,
            new { id, retro, category, text, rootCause, user = SeedIds.Users.Joel });
        return id;
    }

    private static Task InsertThemeAsync(NpgsqlConnection connection, string name)
        => connection.ExecuteAsync(
            "INSERT INTO retro_theme (id, project_id, name, name_set_by, tone) VALUES (@id, @project, @name, 'person', 'bad')",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, name });
}
