using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Store;

// Contract K1: the join between action items and points, Not needed, and merge proposals, on a fresh seeded database.
// Each test uses its own rows so none depends on another.
[TestFixture]
public class RetroActionPointSchemaTests
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Migration_WhenOneToOneActionExists_ShouldCopyItsPointIntoTheJoin()
    {
        await using var connection = await _database.OpenAsync();
        var seed = new RetroSeed(connection, Project);
        var point = await seed.PointAsync(await RetroAsync(seed), new RetroSeedPoint(RetroCategory.Improve, "Legacy."));
        var action = await ActionItemAsync(connection);
        await connection.ExecuteAsync("INSERT INTO retro_action (work_item_id, project_id, point_id) VALUES (@action, @Project, @point)", new { action, Project, point });
        var migration = await File.ReadAllTextAsync(Path.Combine(RepoPaths.Root, "db", "migrations", "202611040900_retros_decisions.sql"));
        var backCopy = Regex.Match(migration, @"INSERT INTO retro_action_point.*?;", RegexOptions.Singleline).Value;
        backCopy.ShouldContain("FROM retro_action WHERE point_id IS NOT NULL");

        await connection.ExecuteAsync(backCopy);

        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE work_item_id = @action AND point_id = @point AND archived_at IS NULL", new { action, point })).ShouldBe(1);
    }

    [Test]
    public async Task Insert_WhenSecondLiveLinkSamePoint_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var seed = new RetroSeed(connection, Project);
        var point = await seed.PointAsync(await RetroAsync(seed), new RetroSeedPoint(RetroCategory.Improve, "Once."));
        await seed.ActionAsync(new RetroSeedAction(await ActionItemAsync(connection), PointId: point));
        var second = await ActionItemAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(second));

        var error = await Should.ThrowAsync<PostgresException>(() => LinkAsync(connection, second, point));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_retro_action_point_live");
    }

    [Test]
    public async Task Insert_WhenSecondLinkAfterArchive_ShouldBeAllowed()
    {
        await using var connection = await _database.OpenAsync();
        var seed = new RetroSeed(connection, Project);
        var point = await seed.PointAsync(await RetroAsync(seed), new RetroSeedPoint(RetroCategory.Improve, "Taken over."));
        var first = await ActionItemAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(first, PointId: point));
        var second = await ActionItemAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(second));
        await connection.ExecuteAsync("UPDATE retro_action_point SET archived_at = now() WHERE work_item_id = @first AND point_id = @point", new { first, point });

        await LinkAsync(connection, second, point);

        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM retro_action_point WHERE point_id = @point", new { point })).ShouldBe(2);
    }

    [Test]
    public async Task Insert_WhenSameMergePairReversed_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var seed = new RetroSeed(connection, Project);
        var one = await seed.ThemeAsync($"Testing {Guid.NewGuid():N}");
        var other = await seed.ThemeAsync($"Test reliability {Guid.NewGuid():N}");
        await ProposeAsync(connection, one, other);

        var error = await Should.ThrowAsync<PostgresException>(() => ProposeAsync(connection, other, one));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_retro_merge_pair");
    }

    [Test]
    public async Task Insert_WhenNotNeededOnWentWell_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await RetroAsync(seed);

        var error = await Should.ThrowAsync<PostgresException>(() => seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Fine.", NotNeededReason: "No need.")));

        error.ConstraintName.ShouldBe("ck_retro_point_not_needed");
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Broke.", NotNeededReason: "One-off."));
    }

    private static Task<Guid> RetroAsync(RetroSeed seed)
        => seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, SprintId: SeedIds.Sprints.Sprint24, AgentName: $"agent-{Guid.NewGuid():N}"));

    private static Task<Guid> ActionItemAsync(NpgsqlConnection connection)
        => new WorkItemBuilder(Project).WithType("action").WithTitle($"Fix {Guid.NewGuid():N}").InsertAsync(connection);

    private static Task LinkAsync(NpgsqlConnection connection, Guid action, Guid point)
        => connection.ExecuteAsync("INSERT INTO retro_action_point (work_item_id, point_id) VALUES (@action, @point)", new { action, point });

    private static Task ProposeAsync(NpgsqlConnection connection, Guid from, Guid into)
        => connection.ExecuteAsync(
            "INSERT INTO retro_theme_merge_proposal (id, project_id, from_theme_id, into_theme_id, reason, proposed_by_user_id) VALUES (@id, @Project, @from, @into, 'Same thing.', @user)",
            new { id = Guid.NewGuid(), Project, from, into, user = SeedIds.Users.Joel });
}
