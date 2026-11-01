using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Search;

// Search read back through IRetroSearchReader. Every test arranges a project of its own with weekly sprints that all ended in March 2026
// (Monday 2 March is S1), so "ended" never depends on the day the test runs.
public sealed class RetroSearchReaderTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;
    private static readonly DateOnly FirstMonday = new(2026, 3, 2);

    private IRetroSearchReader Reader => Host.Services.GetRequiredService<IRetroSearchReader>();

    [Test]
    public async Task Search_WhenWordInRootCause_ShouldFindPoint()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var hit = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "The build was slow.", RootCause: "Stale snapshot baselines kept failing."));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Standup ran long."));

        var result = await Reader.SearchAsync(world.Project.Id, Query("baselines"), CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([hit]);
        result.Total.ShouldBe(1);
    }

    [Test]
    public async Task Search_WhenWordOnlyInFailureLog_ShouldReturnRetroOnce()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, AgentName: "lane A", SprintId: world.Sprints[0]));
        var first = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Tests were green.", Position: 0));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Review took a day.", Position: 1));
        await world.Seed.FailureAsync(retro, RetroFailureKind.Blocker, "Kafka proxy timed out twice.");
        await world.Seed.FailureAsync(retro, RetroFailureKind.Friction, "Kafka consumer needed a restart.", position: 1);

        var result = await Reader.SearchAsync(world.Project.Id, Query("kafka"), CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([first]);
        result.Rows.Single().RetroId.ShouldBe(retro);
    }

    [Test]
    public async Task Search_WhenThemeName_ShouldFindItsPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Shared-branch merge conflicts");
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var inTheme = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Rebased three times.", ThemeId: theme));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Deploy was late."));

        var result = await Reader.SearchAsync(world.Project.Id, Query("merge CONFLICTS"), CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([inTheme]);
        result.Rows.Single().Theme!.Name.ShouldBe("Shared-branch merge conflicts");
    }

    [Test]
    public async Task Search_WhenItemKey_ShouldFindItsPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var story = await world.ItemAsync("story", "Pay with saved card");
        var other = await world.ItemAsync("story", "Refund flow");
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        var mine = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, WorkItemId: story, SprintId: world.Sprints[0], AgentName: "lane A"));
        var theirs = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, WorkItemId: other, SprintId: world.Sprints[0], AgentName: "lane B"));
        var hit = await world.Seed.PointAsync(mine, new RetroSeedPoint(RetroCategory.WentWell, "Went fine."));
        await world.Seed.PointAsync(theirs, new RetroSeedPoint(RetroCategory.WentWell, "Went fine too."));

        var byKey = await Reader.SearchAsync(world.Project.Id, Query(key), CancellationToken.None);
        var byTitle = await Reader.SearchAsync(world.Project.Id, Query("saved card"), CancellationToken.None);

        byKey.Rows.Select(row => row.Id).ShouldBe([hit]);
        byTitle.Rows.Select(row => row.Id).ShouldBe([hit]);
        byKey.Rows.Single().WorkItem!.Key.ShouldBe(key);
    }

    [Test]
    public async Task Filter_WhenCategoryAndRole_ShouldCombine()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var person = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var agent = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, AgentName: "lane A", SprintId: world.Sprints[1]));
        await world.Seed.PointAsync(person, new RetroSeedPoint(RetroCategory.WentBadly, "Person bad."));
        await world.Seed.PointAsync(agent, new RetroSeedPoint(RetroCategory.WentWell, "Agent good."));
        var hit = await world.Seed.PointAsync(agent, new RetroSeedPoint(RetroCategory.WentBadly, "Agent bad."));

        var result = await Reader.SearchAsync(world.Project.Id, Query(null) with { Category = RetroCategory.WentBadly, Role = RetroRole.SubAgent }, CancellationToken.None);
        var inSprint = await Reader.SearchAsync(world.Project.Id, Query(null) with { Sprint = world.Sprints[0] }, CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([hit]);
        inSprint.Rows.Single().Text.ShouldBe("Person bad.");
    }

    [Test]
    public async Task Filter_WhenLevelFeatures_ShouldOnlyReturnFeatureRetros()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var feature = await world.ItemAsync("feature", "Checkout");
        var story = await world.ItemAsync("story", "Pay");
        var featureRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Conductor, WorkItemId: feature, SprintId: world.Sprints[0], AgentName: "conductor"));
        var storyRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, WorkItemId: story, SprintId: world.Sprints[0], AgentName: "lane A"));
        var personRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var featurePoint = await world.Seed.PointAsync(featureRetro, new RetroSeedPoint(RetroCategory.WentWell, "Feature point."));
        var storyPoint = await world.Seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentWell, "Story point."));
        var personPoint = await world.Seed.PointAsync(personRetro, new RetroSeedPoint(RetroCategory.WentWell, "Sprint-level point."));

        var features = await Reader.SearchAsync(world.Project.Id, Query(null) with { Level = RetroLevel.Features }, CancellationToken.None);
        var stories = await Reader.SearchAsync(world.Project.Id, Query(null) with { Level = RetroLevel.Stories }, CancellationToken.None);
        var all = await Reader.SearchAsync(world.Project.Id, Query(null), CancellationToken.None);

        features.Rows.Select(row => row.Id).ShouldBe([featurePoint]);
        features.Rows.Single().IsFeature.ShouldBeTrue();
        stories.Rows.Select(row => row.Id).ShouldBe([storyPoint]);
        all.Rows.Select(row => row.Id).ShouldBe([featurePoint, storyPoint, personPoint], ignoreOrder: true);
    }

    [Test]
    public async Task Filter_WhenRecurringOnly_ShouldUseRules()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var recurring = await world.Seed.ThemeAsync("Slow reviews");
        var once = await world.Seed.ThemeAsync("One-off outage");
        var goodTheme = await world.Seed.ThemeAsync("Clear repro steps", RetroTone.Good);
        var helped = await world.Seed.ThemeAsync("Flaky baselines");
        foreach (var sprint in new[] { 0, 1, 2 })
        {
            await world.PointAsync(sprint, recurring, RetroCategory.WentBadly, $"Slow review {sprint}.");
            await world.PointAsync(sprint, goodTheme, RetroCategory.WentWell, $"Good repro {sprint}.");
            for (var i = 0; i < 3; i++)
            {
                await world.PointAsync(sprint, helped, RetroCategory.WentBadly, $"Flaky {sprint}-{i}.");
            }
        }
        await world.PointAsync(2, once, RetroCategory.WentBadly, "Outage.");
        var fix = await world.ActionAsync(helped, raisedIn: 1);
        await RetroHistory.CompleteAsync(connection, fix, World.NoonIn(2));

        var only = await Reader.SearchAsync(world.Project.Id, Query(null) with { Recurring = true }, CancellationToken.None);
        var everything = await Reader.SearchAsync(world.Project.Id, Query(null), CancellationToken.None);

        only.Rows.Select(row => row.Theme!.Name).Distinct().ShouldBe(["Slow reviews"]);
        only.Total.ShouldBe(3);
        only.Rows.ShouldAllBe(row => row.Recurring);
        everything.Rows.Where(row => row.Recurring).Select(row => row.Theme!.Name).Distinct().ShouldBe(["Slow reviews"]);
    }

    [Test]
    public async Task Search_WhenActionItemRemoved_ShouldDropItFromThePoint()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var point = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Reviews are slow.", ThemeId: theme));
        var action = await world.ActionAsync(theme, raisedIn: 0, pointId: point);

        var before = await Reader.SearchAsync(world.Project.Id, Query(null) with { Selected = retro }, CancellationToken.None);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @projectId AND type = 'action' AND category = 'removed' LIMIT 1) WHERE id = @action", new { projectId = world.Project.Id, action });
        var after = await Reader.SearchAsync(world.Project.Id, Query(null) with { Selected = retro }, CancellationToken.None);

        before.Rows.Single().Action!.WorkItemId.ShouldBe(action);
        after.Rows.Single().Action.ShouldBeNull();
        after.Selected!.Points.Single().Action.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenSelected_ShouldReturnItsCard()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, AgentName: "lane A", SprintId: world.Sprints[0]));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "One."));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Two."));
        await world.Seed.FailureAsync(retro, RetroFailureKind.Blocker, "The log line.");

        var result = await Reader.SearchAsync(world.Project.Id, Query("One") with { Selected = retro }, CancellationToken.None);

        result.Rows.Count.ShouldBe(1);
        result.Selected!.Id.ShouldBe(retro);
        result.Selected.Points.Count.ShouldBe(2);
        result.Selected.FailureLog.Blockers.ShouldBe(["The log line."]);
        result.Sprints.Count.ShouldBe(world.Sprints.Count);
    }

    [Test]
    public async Task Get_WhenArchivedPoint_ShouldNotMatch()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Archived zebra.", Archived: true));
        var archivedRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[1]));
        await world.Seed.PointAsync(archivedRetro, new RetroSeedPoint(RetroCategory.WentBadly, "Zebra in a gone retro."));
        await connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @archivedRetro", new { archivedRetro });

        var matched = await Reader.SearchAsync(world.Project.Id, Query("zebra") with { Selected = archivedRetro }, CancellationToken.None);
        var listed = await Reader.SearchAsync(world.Project.Id, Query(null), CancellationToken.None);

        matched.Rows.ShouldBeEmpty();
        matched.Total.ShouldBe(0);
        matched.Selected.ShouldBeNull();
        listed.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenOtherProject_ShouldNotLeak()
    {
        await using var connection = await OpenDatabaseAsync();
        var mine = await WorldAsync(connection);
        var theirs = await WorldAsync(connection);
        var retro = await theirs.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: theirs.Sprints[0]));
        await theirs.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Secret walrus."));
        await theirs.Seed.ThemeAsync("Walrus theme");

        var result = await Reader.SearchAsync(mine.Project.Id, Query("walrus") with { Selected = retro }, CancellationToken.None);

        result.Rows.ShouldBeEmpty();
        result.Selected.ShouldBeNull();
        result.Themes.ShouldBeEmpty();
        result.Sprints.Select(sprint => sprint.Id).ShouldBe(mine.Sprints, ignoreOrder: true);
    }

    [TestCase("category=bogus")]
    [TestCase("level=bogus")]
    [TestCase("role=bogus")]
    public async Task Get_WhenUnknownCategoryOrLevelOrRole_ShouldBeValidation(string query)
    {
        using var response = await Api.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/retros?{query}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Search_WhenPointAndFailureLogBothMatch_ShouldReturnOnlyTheMatchingPoint()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, AgentName: "lane A", SprintId: world.Sprints[0]));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Tests were green.", Position: 0));
        var hit = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Kafka proxy was down.", Position: 1));
        await world.Seed.FailureAsync(retro, RetroFailureKind.Blocker, "Kafka proxy timed out twice.");

        var result = await Reader.SearchAsync(world.Project.Id, Query("kafka"), CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([hit]);
        result.Total.ShouldBe(1);
    }

    [Test]
    public async Task Search_WhenQueryHasPercentAndUnderscore_ShouldMatchThemLiterally()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var literal = await world.Seed.ThemeAsync("Flaky 100%_ci runs");
        var other = await world.Seed.ThemeAsync("Flaky 100 ci runs");
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var hit = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Reran twice.", ThemeId: literal));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Reran thrice.", ThemeId: other));

        var percent = await Reader.SearchAsync(world.Project.Id, Query("100%_ci"), CancellationToken.None);
        var wildcard = await Reader.SearchAsync(world.Project.Id, Query("%"), CancellationToken.None);

        percent.Rows.Select(row => row.Id).ShouldBe([hit]);
        wildcard.Rows.Select(row => row.Id).ShouldBe([hit]);
    }

    [Test]
    public async Task Filter_WhenTheme_ShouldOnlyReturnItsPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow CI");
        var otherTheme = await world.Seed.ThemeAsync("Late reviews");
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var hit = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Pipeline crawled.", ThemeId: theme));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Review waited.", ThemeId: otherTheme));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "No theme."));

        var result = await Reader.SearchAsync(world.Project.Id, Query(null) with { Theme = theme }, CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([hit]);
    }

    [Test]
    public async Task Search_WhenNoQuery_ShouldListNewestPointFirstAndOnlyNonEmptyThemes()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var used = await world.Seed.ThemeAsync("Used theme");
        await world.Seed.ThemeAsync("Empty theme");
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var older = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Older.", ThemeId: used, CreatedAt: World.NoonIn(0)));
        var newer = await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Newer.", Position: 1, CreatedAt: World.NoonIn(1)));

        var result = await Reader.SearchAsync(world.Project.Id, Query(null), CancellationToken.None);

        result.Rows.Select(row => row.Id).ShouldBe([newer, older]);
        result.Themes.Select(theme => theme.Name).ShouldBe(["Used theme"]);
    }

    private static RetroSearchQuery Query(string? q) => new(q, null, null, null, null, null, null, null);

    private static async Task<World> WorldAsync(NpgsqlConnection connection)
    {
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        var sprints = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var start = FirstMonday.AddDays(7 * i);
            sprints.Add(await RetroArrangement.SprintAsync(connection, project.Id, $"S{i + 1}", start, start.AddDays(6)));
        }

        return new World(connection, project, sprints, new RetroSeed(connection, project.Id));
    }

    private sealed record World(NpgsqlConnection Connection, ArrangedProject Project, IReadOnlyList<Guid> Sprints, RetroSeed Seed)
    {
        // Noon on the Wednesday of the nth sprint.
        public static DateTimeOffset NoonIn(int sprint)
        {
            var day = FirstMonday.AddDays((7 * sprint) + 2);
            return RetroArrangement.Noon(day.Year, day.Month, day.Day);
        }

        public async Task<Guid> ItemAsync(string type, string title)
            => await RetroHistory.CreateAsync(Connection, new WorkItemBuilder(Project.Id).WithType(type).WithTitle(title));

        public async Task PointAsync(int sprint, Guid theme, RetroCategory category, string text)
        {
            var retro = await Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: Sprints[sprint]));
            await Seed.PointAsync(retro, new RetroSeedPoint(category, text, ThemeId: theme, CreatedAt: NoonIn(sprint)));
        }

        public async Task<Guid> ActionAsync(Guid theme, int raisedIn, Guid? pointId = null)
        {
            var id = await RetroHistory.CreateAsync(Connection, new WorkItemBuilder(Project.Id).WithType("action").WithTitle($"Fix {Guid.NewGuid():N}"));
            await Seed.ActionAsync(new RetroSeedAction(id, theme, pointId, Sprints[raisedIn]));
            return id;
        }
    }
}
