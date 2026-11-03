using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Reads;

// Contract K6, GET action-dialog: what the dialog selects and offers, read through IRetroActionDialogReader.
public sealed class RetroActionDialogReaderTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;
    private static readonly DateTimeOffset Day = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private IRetroActionDialogReader Reader => Host.Services.GetRequiredService<IRetroActionDialogReader>();

    [Test]
    public async Task ActionDialog_WhenThemeGiven_ShouldSelectThatThemesUndecided()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var theme = await seed.ThemeAsync($"Theme {Guid.NewGuid():N}");
        var other = await seed.ThemeAsync($"Other {Guid.NewGuid():N}");
        var item = await ItemAsync(connection, "story");
        var retro = await AgentRetroAsync(seed, item);
        var first = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "First.", ThemeId: theme, CreatedAt: Day));
        var second = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Second.", ThemeId: theme, CreatedAt: Day.AddHours(1)));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Another theme.", ThemeId: other));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Well.", ThemeId: theme));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Decided.", ThemeId: theme, NotNeededReason: "Not worth it."));

        var dialog = await Reader.GetAsync(Project, [], theme, item, null);

        dialog.Selected.Select(point => point.Id).ShouldBe([first, second]);
        dialog.Offered.Select(point => point.Text).ShouldBe(["Another theme."]);
        dialog.SuggestedTheme.ShouldNotBeNull().Id.ShouldBe(theme);
    }

    [Test]
    public async Task ActionDialog_WhenPointsGiven_ShouldKeepTheirOrderAndSuggestTheCommonestTheme()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var rare = await seed.ThemeAsync($"Rare {Guid.NewGuid():N}");
        var common = await seed.ThemeAsync($"Common {Guid.NewGuid():N}");
        var retro = await AgentRetroAsync(seed, await ItemAsync(connection, "story"));
        var one = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "One.", ThemeId: rare, CreatedAt: Day));
        var two = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Two.", ThemeId: common, CreatedAt: Day.AddHours(1)));
        var three = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.NotInPlan, "Three.", ThemeId: common, CreatedAt: Day.AddHours(2)));

        var dialog = await Reader.GetAsync(Project, [three, one, two], null, null, null);

        dialog.Selected.Select(point => point.Id).ShouldBe([three, one, two]);
        dialog.SuggestedTheme.ShouldNotBeNull().Id.ShouldBe(common);
    }

    [Test]
    public async Task ActionDialog_WhenScopedToItem_ShouldOfferOnlyItsUndecidedInOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var item = await ItemAsync(connection, "story");
        var retro = await AgentRetroAsync(seed, item);
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.NotInPlan, "Not in plan.", CreatedAt: Day));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Badly old.", CreatedAt: Day));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Badly new.", CreatedAt: Day.AddHours(2)));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Improve.", CreatedAt: Day));
        var elsewhere = await AgentRetroAsync(seed, await ItemAsync(connection, "story"));
        await seed.PointAsync(elsewhere, new RetroSeedPoint(RetroCategory.Improve, "Elsewhere."));

        var dialog = await Reader.GetAsync(Project, [], null, item, null);

        dialog.Selected.ShouldBeEmpty();
        dialog.Offered.Select(point => point.Text).ShouldBe(["Improve.", "Badly new.", "Badly old.", "Not in plan."]);
    }

    [Test]
    public async Task ActionDialog_WhenScopedToFeature_ShouldOfferItsStoriesPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var feature = await ItemAsync(connection, "feature");
        var story = await new WorkItemBuilder(Project).WithType("story").WithTitle("Child").WithParent(feature).InsertAsync(connection);
        var bug = await new WorkItemBuilder(Project).WithType("bug").WithTitle("Child bug").WithParent(feature).InsertAsync(connection);
        await seed.PointAsync(await AgentRetroAsync(seed, feature), new RetroSeedPoint(RetroCategory.Improve, "Feature.", CreatedAt: Day));
        await seed.PointAsync(await AgentRetroAsync(seed, story), new RetroSeedPoint(RetroCategory.Improve, "Story.", CreatedAt: Day.AddHours(1)));
        await seed.PointAsync(await AgentRetroAsync(seed, bug), new RetroSeedPoint(RetroCategory.WentBadly, "Bug.", CreatedAt: Day));
        await seed.PointAsync(await AgentRetroAsync(seed, await ItemAsync(connection, "story")), new RetroSeedPoint(RetroCategory.Improve, "Unrelated."));
        var childAction = await new WorkItemBuilder(Project).WithType("action").WithTitle("Child action").WithParent(feature).InsertAsync(connection);
        await seed.PointAsync(await AgentRetroAsync(seed, childAction), new RetroSeedPoint(RetroCategory.Improve, "Action child."));

        var dialog = await Reader.GetAsync(Project, [], null, feature, null);

        dialog.Offered.Select(point => point.Text).ShouldBe(["Story.", "Feature.", "Bug."]);
    }

    [Test]
    public async Task ActionDialog_ShouldLeaveOutArchivedRetrospectives()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var theme = await seed.ThemeAsync($"Theme {Guid.NewGuid():N}");
        var item = await ItemAsync(connection, "story");
        var live = await AgentRetroAsync(seed, item);
        var archived = await AgentRetroAsync(seed, item);
        var kept = await seed.PointAsync(live, new RetroSeedPoint(RetroCategory.Improve, "Live.", ThemeId: theme));
        var gone = await seed.PointAsync(archived, new RetroSeedPoint(RetroCategory.Improve, "Archived retro.", ThemeId: theme));
        await connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @archived", new { archived });

        var dialog = await Reader.GetAsync(Project, [gone], theme, item, null);

        dialog.Selected.Select(point => point.Id).ShouldBe([kept]);
        dialog.Offered.ShouldBeEmpty();
    }

    [Test]
    public async Task ActionDialog_ShouldLeaveOutWentWellAndDecided()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var item = await ItemAsync(connection, "story");
        var retro = await AgentRetroAsync(seed, item);
        var open = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Open."));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Well."));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Not needed.", NotNeededReason: "Nothing to do."));
        var actioned = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Actioned."));
        var action = await new WorkItemBuilder(Project).WithType("action").WithTitle("Fix it").InsertAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(action, PointId: actioned));

        var dialog = await Reader.GetAsync(Project, [actioned], null, item, null);

        dialog.Offered.Select(point => point.Id).ShouldBe([open]);
        dialog.Selected.ShouldBeEmpty();
    }

    [Test]
    public async Task ActionDialog_ShouldListOpenActionsOnly()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var live = await new WorkItemBuilder(Project).WithType("action").WithTitle("Live action").InsertAsync(connection);
        var removed = await new WorkItemBuilder(Project).WithType("action").WithTitle("Removed action").WithStateNamed("Removed").InsertAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(live, FixType: RetroFixType.Test));
        await seed.ActionAsync(new RetroSeedAction(removed));

        var dialog = await Reader.GetAsync(Project, [], null, null, null);

        var found = dialog.OpenActions.Single(action => action.WorkItemId == live);
        found.FixType.ShouldBe(RetroFixType.Test);
        dialog.OpenActions.Select(action => action.WorkItemId).ShouldNotContain(removed);
    }

    private static Task<Guid> ItemAsync(Npgsql.NpgsqlConnection connection, string type)
        => new WorkItemBuilder(Project).WithType(type).WithTitle($"{type} {Guid.NewGuid():N}").InsertAsync(connection);

    private static Task<Guid> AgentRetroAsync(RetroSeed seed, Guid item)
        => seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: item, AgentName: $"agent-{Guid.NewGuid():N}"));
}
