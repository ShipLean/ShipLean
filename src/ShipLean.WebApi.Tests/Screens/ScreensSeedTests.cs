using Dapper;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Screens;

namespace ShipLean.WebApi.Tests.Screens;

// The phone screens' seed (db/seed/0014_phone_screens.sql, SeedIds.Screens): read only.
[TestFixture]
public class ScreensSeedTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Seed_WhenLoaded_ShouldGiveTomTheScreensSeed()
    {
        await using var connection = await _database.OpenAsync();

        (await ScreensRows.HealthAsync(connection, SeedIds.Screens.AtRiskFeature))
            .ShouldBe(new ScreensRows.Health("atRisk", TestDates.Today.AddDays(-1), SeedIds.Users.Tom, null));
        (await ScreensRows.HealthAsync(connection, SeedIds.Screens.BlockedFeature))
            .ShouldBe(new ScreensRows.Health("blocked", TestDates.Today.AddDays(-1), SeedIds.Users.Tom, null));
        var features = (await connection.QueryAsync<(Guid Id, string Type, Guid? Assignee, string? StatusLine)>(
            "SELECT id, type, assigned_to, status_line FROM work_item WHERE id = ANY(@ids) ORDER BY id",
            new { ids = new[] { SeedIds.Screens.AtRiskFeature, SeedIds.Screens.BlockedFeature } })).ToList();
        features.ShouldAllBe(f => f.Type == "feature" && f.Assignee == SeedIds.Users.Tom);
        features.Single(f => f.Id == SeedIds.Screens.BlockedFeature).StatusLine.ShouldNotBeNull().ShouldContain("blocked");

        var retro = await connection.QuerySingleAsync<(Guid Author, Guid? Sprint, Guid? Item, string Role)>(
            "SELECT author_user_id, sprint_id, work_item_id, role FROM retrospective WHERE id = @id", new { id = SeedIds.Screens.Retro });
        retro.ShouldBe((SeedIds.Users.Joel, (Guid?)SeedIds.Sprints.Sprint24, (Guid?)null, "person"));
        (await connection.QuerySingleAsync<(Guid Retro, string Category)>(
            "SELECT retrospective_id, category FROM retro_point WHERE id = @id", new { id = SeedIds.Screens.RetroPoint }))
            .ShouldBe((SeedIds.Screens.Retro, "improve"));
        var support = (await ScreensRows.SupportAsync(connection, SeedIds.Screens.RetroPoint)).ShouldHaveSingleItem();
        (support.Id, support.UserId, support.WithdrawnAt).ShouldBe((SeedIds.Screens.RetroSupport, SeedIds.Users.Priya, (DateTimeOffset?)null));

        (await ScreensRows.DigestSeenAsync(connection, SeedIds.Screens.DigestSeenUser, SeedIds.Screens.DigestSeenProject))
            .ShouldBe(TestDates.Today.AddDays(-2));
    }
}
