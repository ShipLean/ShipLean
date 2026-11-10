using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.Screens;

namespace ShipLean.WebApi.Tests.Screens;

// The phone screens' tables (N1: work_item.health, retro_point_support, feature_digest_seen) and the builders their server tasks
// arrange with. The database is shared by the class, so every test arranges its own work item or point. The
// seed has Tom's digest-seen row for SHIP only, so the upsert test uses PAY.
[TestFixture]
public class ScreensSqlTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Health_WhenSetOnAStory_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => connection.ExecuteAsync(
            "UPDATE work_item SET health = 'atRisk', health_set_at = now() WHERE id = @story", new { story }));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_work_item_health_feature_only");
    }

    [Test]
    public async Task Health_WhenUnknownValue_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
        await connection.ExecuteAsync(
            "UPDATE work_item SET health = 'onTrack', health_set_at = now(), health_set_by = @tom WHERE id = @feature",
            new { feature, tom = SeedIds.Users.Tom });

        var error = await Should.ThrowAsync<PostgresException>(() => connection.ExecuteAsync(
            "UPDATE work_item SET health = 'late' WHERE id = @feature", new { feature }));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("work_item_health_check");
        var health = await ScreensRows.HealthAsync(connection, feature);
        (health.Value, health.SetBy, health.SetAgent).ShouldBe(("onTrack", (Guid?)SeedIds.Users.Tom, (string?)null));
    }

    [Test]
    public async Task RetroSupport_WhenSameUserSupportsTwiceLive_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var point = await PointAsync(connection);
        await new RetroSupportBuilder(point, SeedIds.Users.Priya).InsertAsync(connection);

        var error = await Should.ThrowAsync<PostgresException>(() => new RetroSupportBuilder(point, SeedIds.Users.Priya).InsertAsync(connection));

        error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        error.ConstraintName.ShouldBe("ux_retro_point_support_live");
    }

    [Test]
    public async Task RetroSupport_WhenWithdrawnThenSupportedAgain_ShouldBeAccepted()
    {
        await using var connection = await _database.OpenAsync();
        var point = await PointAsync(connection);
        var supportedAt = TestDates.Today.AddDays(-2).AddHours(9);
        var withdrawnAt = TestDates.Today.AddDays(-1).AddHours(10);
        var first = await new RetroSupportBuilder(point, SeedIds.Users.Priya).WithCreatedAt(supportedAt).WithWithdrawnAt(withdrawnAt).InsertAsync(connection);
        var again = await new RetroSupportBuilder(point, SeedIds.Users.Priya).WithCreatedAt(TestDates.Today.AddHours(8)).InsertAsync(connection);

        (await ScreensRows.SupportAsync(connection, point)).ShouldBe([
            new ScreensRows.Support(first, point, SeedIds.Users.Priya, supportedAt, withdrawnAt),
            new ScreensRows.Support(again, point, SeedIds.Users.Priya, TestDates.Today.AddHours(8), null),
        ]);
    }

    [Test]
    public async Task DigestSeen_WhenUpsertedTwice_ShouldKeepOneRowWithTheLaterTime()
    {
        await using var connection = await _database.OpenAsync();
        var project = SeedIds.Projects.Pay;
        await new DigestSeenBuilder(SeedIds.Users.Tom, project).WithSeenAt(TestDates.Today.AddDays(-3)).InsertAsync(connection);

        await new DigestSeenBuilder(SeedIds.Users.Tom, project).WithSeenAt(TestDates.Today.AddHours(9)).InsertAsync(connection);

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM feature_digest_seen WHERE project_id = @project", new { project })).ShouldBe(1);
        (await ScreensRows.DigestSeenAsync(connection, SeedIds.Users.Tom, project)).ShouldBe(TestDates.Today.AddHours(9));
    }

    [Test]
    public async Task DigestSeen_WhenAnEarlierTimeIsInsertedSecond_ShouldKeepTheLaterTime()
    {
        await using var connection = await _database.OpenAsync();
        var project = SeedIds.Projects.Ship;
        await new DigestSeenBuilder(SeedIds.Users.Priya, project).WithSeenAt(TestDates.Today.AddHours(9)).InsertAsync(connection);

        await new DigestSeenBuilder(SeedIds.Users.Priya, project).WithSeenAt(TestDates.Today.AddDays(-3)).InsertAsync(connection);

        (await ScreensRows.DigestSeenAsync(connection, SeedIds.Users.Priya, project)).ShouldBe(TestDates.Today.AddHours(9));
    }

    // A fresh improve point on a person retro of Joel's on Sprint 24, so tests never share a point.
    private static async Task<Guid> PointAsync(NpgsqlConnection connection)
    {
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: SeedIds.Sprints.Sprint24));
        return await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Pair on the hard parts."));
    }
}
