using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Insights;

// Insights read back through IRetroInsightsReader. Every test arranges a project of its own with weekly sprints that all ended in
// March 2026 (Monday 2 March is S1), so "ended" never depends on the day the test runs.
public sealed class RetroInsightsReaderTests : ApiFixture
{
    private static readonly DateOnly FirstMonday = new(2026, 3, 2);

    private IRetroInsightsReader Reader => Host.Services.GetRequiredService<IRetroInsightsReader>();

    [Test]
    public async Task Get_WhenThemeIn3Of4Sprints_ShouldBeRecurring()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 4);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(1, theme, 1);
        await world.MentionAsync(2, theme, 2);
        await world.MentionAsync(3, theme, 1);

        var row = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Themes.Single();

        row.Status.ShouldBe(RetroThemeStatus.Recurring);
        row.Counts.ShouldBe([0, 1, 2, 1]);
        row.Total.ShouldBe(4);
        row.LastVerdict.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenOpenActionItem_ShouldBeBeingAddressed()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 4);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 2);
        var action = await world.ActionAsync(theme, raisedIn: 0);

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);

        result.Themes.Single().Status.ShouldBe(RetroThemeStatus.BeingAddressed);
        result.Themes.Single().ActionItems.Single().WorkItemId.ShouldBe(action);
        result.ActionItems.Single().Impact.ShouldBeNull();
        result.Impact.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenClosedActionHelped_ShouldBeResolvedWithHelpedImpact()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 5);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 3);
        await world.MentionAsync(1, theme, 3);
        await world.MentionAsync(2, theme, 3);
        var action = await world.ActionAsync(theme, raisedIn: 1);
        await RetroHistory.CompleteAsync(connection, action, World.NoonIn(2));

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);

        var row = result.Themes.Single();
        row.Status.ShouldBe(RetroThemeStatus.Resolved);
        row.LastVerdict.ShouldBe(RetroVerdict.Helped);
        var impact = result.Impact.Single();
        impact.Verdict.ShouldBe(RetroVerdict.Helped);
        impact.Counts.ShouldBe([3, 3, 3, 0, 0]);
        impact.ClosedAt.ShouldBe(2);
        impact.BeforeAvg.ShouldBe(3d);
        impact.AfterAvg.ShouldBe(0d);
        impact.ChangePct.ShouldBe(-1d);
        impact.LatestMention.ShouldBeNull();
        result.ActionItems.Single().Impact.ShouldBe(RetroVerdict.Helped);
    }

    [Test]
    public async Task Get_WhenNoSprintSinceClose_ShouldBeTooEarly()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 3);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(1, theme, 2);
        await world.MentionAsync(2, theme, 2);
        var action = await world.ActionAsync(theme, raisedIn: 1);
        await RetroHistory.CompleteAsync(connection, action, World.NoonIn(2));

        var impact = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Impact.Single();

        impact.Verdict.ShouldBe(RetroVerdict.TooEarly);
        impact.AfterAvg.ShouldBeNull();
        impact.ChangePct.ShouldBeNull();
        impact.BeforeAvg.ShouldBe(4d / 3);
    }

    [Test]
    public async Task Get_WhenNoEffect_ShouldNameLatestMention()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 5);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 1);
        await world.MentionAsync(1, theme, 1);
        await world.MentionAsync(2, theme, 2);
        await world.MentionAsync(3, theme, 2);
        await world.MentionAsync(4, theme, 2, text: "The newest one.");
        var action = await world.ActionAsync(theme, raisedIn: 0);
        await RetroHistory.CompleteAsync(connection, action, World.NoonIn(1));

        var impact = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Impact.Single();

        impact.Verdict.ShouldBe(RetroVerdict.NoEffect);
        impact.ChangePct.ShouldBe(1d);
        impact.LatestMention.ShouldNotBeNull().Text.ShouldBe("The newest one.");
        impact.LatestMention.Recurring.ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenBeforeIsZero_ShouldBeNoEffectWithoutChangePct()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 4);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(3, theme, 1);
        var action = await world.ActionAsync(theme, raisedIn: 0);
        await RetroHistory.CompleteAsync(connection, action, World.NoonIn(1));

        var impact = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Impact.Single();

        impact.Verdict.ShouldBe(RetroVerdict.NoEffect);
        impact.BeforeAvg.ShouldBe(0d);
        impact.ChangePct.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenRange3_ShouldReturnThreeSprintColumnsButSameVerdicts()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 6);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 3);
        await world.MentionAsync(1, theme, 3);
        await world.MentionAsync(2, theme, 3);
        await world.MentionAsync(4, theme, 1);
        var action = await world.ActionAsync(theme, raisedIn: 1);
        await RetroHistory.CompleteAsync(connection, action, World.NoonIn(2));

        var wide = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);
        var narrow = await Reader.GetAsync(world.Project.Id, RetroRange.Last3, CancellationToken.None);

        wide.Range.ShouldBe("6");
        wide.Sprints.Count.ShouldBe(6);
        narrow.Range.ShouldBe("3");
        narrow.Sprints.Select(sprint => sprint.Id).ShouldBe(world.Sprints.Skip(3));
        narrow.Themes.Single().Counts.ShouldBe([0, 1, 0]);
        narrow.Themes.Single().Total.ShouldBe(1);
        narrow.Themes.Single().LastVerdict.ShouldBe(wide.Themes.Single().LastVerdict);
        var (wideImpact, narrowImpact) = (wide.Impact.Single(), narrow.Impact.Single());
        narrowImpact.Verdict.ShouldBe(wideImpact.Verdict);
        narrowImpact.BeforeAvg.ShouldBe(wideImpact.BeforeAvg);
        narrowImpact.AfterAvg.ShouldBe(wideImpact.AfterAvg);
        narrowImpact.Counts.ShouldBe([0, 1, 0]);
        narrowImpact.ClosedAt.ShouldBe(-1);
        wideImpact.ClosedAt.ShouldBe(2);
    }

    [Test]
    public async Task Get_WhenCurrentSprintExists_ShouldShowItButNotJudgeIt()
    {
        await using var connection = await OpenDatabaseAsync();
        var today = DateOnly.FromDateTime(Host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        var currentStart = today.AddDays(-2);
        var starts = new[] { currentStart.AddDays(-21), currentStart.AddDays(-14), currentStart.AddDays(-7), currentStart };
        var world = await WorldAsync(connection, starts);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 3);
        await world.MentionAsync(1, theme, 3);
        await world.MentionAsync(2, theme, 3);
        await world.MentionAsync(3, theme, 1, text: "Mentioned in the current sprint.");
        var action = await world.ActionAsync(theme, raisedIn: 1);
        var closedOn = starts[2].AddDays(1);
        await RetroHistory.CompleteAsync(connection, action, RetroArrangement.Noon(closedOn.Year, closedOn.Month, closedOn.Day));

        var wide = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);
        var narrow = await Reader.GetAsync(world.Project.Id, RetroRange.Last3, CancellationToken.None);

        wide.Sprints.Count.ShouldBe(4);
        wide.Sprints[^1].Status.ShouldBe(SprintStatus.Current);
        wide.Impact.Single().Verdict.ShouldBe(RetroVerdict.TooEarly);
        wide.Impact.Single().AfterAvg.ShouldBeNull();
        narrow.Sprints.Select(sprint => sprint.Id).ShouldBe(world.Sprints.Skip(1));
        narrow.Themes.Single().Counts.ShouldBe([3, 3, 1]);
        narrow.Themes.Single().Latest.ShouldBe("Mentioned in the current sprint.");
        narrow.Impact.Single().Verdict.ShouldBe(RetroVerdict.TooEarly);
        narrow.Impact.Single().ClosedAt.ShouldBe(1);
    }

    [Test]
    public async Task Get_WhenPointsOrRetroAreArchived_ShouldNotCountThem()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 3);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        var live = await world.Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        await world.Seed.PointAsync(live, new RetroSeedPoint(RetroCategory.WentBadly, "Live point.", ThemeId: theme, CreatedAt: World.NoonIn(0)));
        await world.Seed.PointAsync(live, new RetroSeedPoint(RetroCategory.WentBadly, "Archived point.", ThemeId: theme, CreatedAt: World.NoonIn(0).AddHours(1), Archived: true));
        var archivedRetro = await world.Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: world.Sprints[1]));
        await world.Seed.PointAsync(archivedRetro, new RetroSeedPoint(RetroCategory.WentBadly, "In an archived retro.", ThemeId: theme, CreatedAt: World.NoonIn(1)));
        await connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @archivedRetro", new { archivedRetro });

        var row = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Themes.Single();

        row.Counts.ShouldBe([1, 0, 0]);
        row.Total.ShouldBe(1);
        row.Latest.ShouldBe("Live point.");
    }

    [Test]
    public async Task Get_WhenActionItemRemoved_ShouldDropOutOfEveryView()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 3);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 1);
        var action = await world.ActionAsync(theme, raisedIn: 0);
        await connection.ExecuteAsync(
            "UPDATE work_item w SET state_id = (SELECT s.id FROM state s WHERE s.project_id = w.project_id AND s.type = w.type AND s.category = 'removed') WHERE w.id = @action",
            new { action });

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);

        result.ActionItems.ShouldBeEmpty();
        result.Themes.Single().ActionItems.ShouldBeEmpty();
        result.Themes.Single().Status.ShouldBe(RetroThemeStatus.Quiet);
    }

    [Test]
    public async Task Get_WhenGoodTone_ShouldBeInKeepsWorking()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 3);
        var theme = await world.Seed.ThemeAsync("Contracts first", RetroTone.Good);
        await world.MentionAsync(0, theme, 2, RetroCategory.WentWell);
        await world.MentionAsync(2, theme, 1, RetroCategory.WentWell);

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);

        result.Themes.ShouldBeEmpty();
        var row = result.KeepsWorking.Single();
        row.Counts.ShouldBe([2, 0, 1]);
        row.Total.ShouldBe(3);
        row.Status.ShouldBe(RetroThemeStatus.Quiet);
        row.Latest.ShouldBe("Point 2-0");
    }

    [Test]
    public async Task Get_WhenActionPlannedInSprint_ShouldShowPlannedSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 4);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(0, theme, 1);
        await world.ActionAsync(theme, raisedIn: 0, plannedIn: 3, owner: SeedIds.Users.Joel);

        var row = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).ActionItems.Single();

        row.Action.Sprint.ShouldNotBeNull().Id.ShouldBe(world.Sprints[3]);
        row.RaisedIn.ShouldNotBeNull().Id.ShouldBe(world.Sprints[0]);
        row.Owner.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Joel);
        row.Theme.ShouldNotBeNull().Id.ShouldBe(theme);
    }

    [Test]
    public async Task Get_WhenThemesAreMixed_ShouldOrderByStatusThenTotal()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 4);
        var quiet = await world.Seed.ThemeAsync("Quiet big");
        var recurring = await world.Seed.ThemeAsync("Recurring small");
        var addressed = await world.Seed.ThemeAsync("Addressed");
        await world.MentionAsync(0, quiet, 9);
        await world.MentionAsync(1, recurring, 1);
        await world.MentionAsync(2, recurring, 1);
        await world.MentionAsync(3, recurring, 1);
        await world.MentionAsync(0, addressed, 1);
        await world.ActionAsync(addressed, raisedIn: 0);

        var themes = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Themes;

        themes.Select(row => row.Theme.Id).ShouldBe([recurring, addressed, quiet]);
    }

    [Test]
    public async Task Get_WhenNoSprintPoints_ShouldCountInTotalNotRecurring()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 4);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(1, theme, 1);
        await world.MentionAsync(2, theme, 1);
        await world.NoSprintAsync(theme, 3);
        var alone = await world.Seed.ThemeAsync("Only without a sprint");
        await world.NoSprintAsync(alone, 3);
        var quiet = await world.Seed.ThemeAsync("Verdict untouched");
        await world.NoSprintAsync(quiet, 5);
        var action = await world.ActionAsync(quiet, raisedIn: 0);
        await RetroHistory.CompleteAsync(connection, action, World.NoonIn(1));

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);

        var row = result.Themes.Single(row => row.Theme.Id == theme);
        row.Counts.ShouldBe([0, 1, 1, 0]);
        row.NoSprint.ShouldBe(3);
        row.Total.ShouldBe(5);
        row.Status.ShouldBe(RetroThemeStatus.Quiet);
        var onlyNoSprint = result.Themes.Single(row => row.Theme.Id == alone);
        onlyNoSprint.Counts.ShouldBe([0, 0, 0, 0]);
        onlyNoSprint.Total.ShouldBe(3);
        onlyNoSprint.Status.ShouldBe(RetroThemeStatus.Quiet);
        var impact = result.Impact.Single();
        impact.BeforeAvg.ShouldBe(0d);
        impact.AfterAvg.ShouldBe(0d);
        impact.Counts.ShouldBe([0, 0, 0, 0]);
    }

    [Test]
    public async Task Get_WhenNoSprintOnly_ShouldShowLatestText()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 2);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        var retro = await world.NoSprintRetroAsync();
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Older.", ThemeId: theme, CreatedAt: World.NoonIn(0)));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.NotInPlan, "Newest.", ThemeId: theme, CreatedAt: World.NoonIn(1)));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Not a mention.", ThemeId: theme, CreatedAt: World.NoonIn(1).AddDays(1)));

        var row = (await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None)).Themes.Single();

        row.Latest.ShouldBe("Newest.");
        row.NoSprint.ShouldBe(2);
        row.Total.ShouldBe(2);
    }

    [Test]
    public async Task Get_WhenUnthemed_ShouldCountThemLastAndLeaveOutWentWell()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection, 3);
        var sprint1 = await world.Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: world.Sprints[0]));
        var sprint2 = await world.Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: world.Sprints[1]));
        var none = await world.NoSprintRetroAsync();
        await world.Seed.PointAsync(sprint1, new RetroSeedPoint(RetroCategory.WentBadly, "A.", CreatedAt: World.NoonIn(0)));
        await world.Seed.PointAsync(sprint1, new RetroSeedPoint(RetroCategory.NotInPlan, "B.", CreatedAt: World.NoonIn(0)));
        await world.Seed.PointAsync(sprint1, new RetroSeedPoint(RetroCategory.WentWell, "Not counted.", CreatedAt: World.NoonIn(0)));
        await world.Seed.PointAsync(sprint2, new RetroSeedPoint(RetroCategory.Improve, "C.", CreatedAt: World.NoonIn(1)));
        await world.Seed.PointAsync(none, new RetroSeedPoint(RetroCategory.WentBadly, "D.", CreatedAt: World.NoonIn(1)));
        await world.Seed.PointAsync(none, new RetroSeedPoint(RetroCategory.WentWell, "Not counted either.", CreatedAt: World.NoonIn(1)));

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Last6, CancellationToken.None);

        result.Unthemed.Counts.ShouldBe([2, 1, 0]);
        result.Unthemed.NoSprint.ShouldBe(1);
        result.Unthemed.Total.ShouldBe(4);
        result.Themes.ShouldBeEmpty();
        result.KeepsWorking.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenNoRetros_ShouldReturnEmptyListsNotNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);

        var result = await Reader.GetAsync(project.Id, RetroRange.All, CancellationToken.None);

        result.Range.ShouldBe("all");
        result.Sprints.ShouldBeEmpty();
        result.Themes.ShouldBeEmpty();
        result.KeepsWorking.ShouldBeEmpty();
        result.ActionItems.ShouldBeEmpty();
        result.Impact.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenRangeInvalid_ShouldBeValidation()
    {
        using var response = await Api.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/retros/insights?range=12");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private static Task<World> WorldAsync(NpgsqlConnection connection, int sprintCount)
        => WorldAsync(connection, [.. Enumerable.Range(0, sprintCount).Select(i => FirstMonday.AddDays(7 * i))]);

    private static async Task<World> WorldAsync(NpgsqlConnection connection, IReadOnlyList<DateOnly> starts)
    {
        var project = await ArrangedProject.CreateAsync(connection);
        var sprints = new List<Guid>();
        for (var i = 0; i < starts.Count; i++)
        {
            sprints.Add(await RetroArrangement.SprintAsync(connection, project.Id, $"S{i + 1}", starts[i], starts[i].AddDays(6)));
        }

        return new World(connection, project, sprints, new RetroSeed(connection, project.Id));
    }

    internal sealed record World(NpgsqlConnection Connection, ArrangedProject Project, IReadOnlyList<Guid> Sprints, RetroSeed Seed)
    {
        // Noon on the Wednesday of the nth sprint.
        public static DateTimeOffset NoonIn(int sprint)
        {
            var day = FirstMonday.AddDays((7 * sprint) + 2);
            return RetroArrangement.Noon(day.Year, day.Month, day.Day);
        }

        public async Task MentionAsync(int sprint, Guid theme, int count, RetroCategory category = RetroCategory.WentBadly, string? text = null)
        {
            var retro = await Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: Sprints[sprint]));
            for (var i = 0; i < count; i++)
            {
                await Seed.PointAsync(retro, new RetroSeedPoint(category, text ?? $"Point {sprint}-{i}", ThemeId: theme, CreatedAt: World.NoonIn(sprint).AddMinutes(i)));
            }
        }

        // A retro on a story, with no sprint: a retro needs one or the other.
        public async Task<Guid> NoSprintRetroAsync()
        {
            var story = await RetroHistory.CreateAsync(Connection, new WorkItemBuilder(Project.Id).WithType("story").WithTitle($"Story {Guid.NewGuid():N}"));
            return await Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: story));
        }

        public async Task NoSprintAsync(Guid theme, int count)
        {
            var retro = await NoSprintRetroAsync();
            for (var i = 0; i < count; i++)
            {
                await Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"No sprint {i}", ThemeId: theme, CreatedAt: World.NoonIn(0).AddMinutes(i)));
            }
        }

        public async Task<Guid> ActionAsync(Guid theme, int raisedIn, int? plannedIn = null, Guid? owner = null)
        {
            var builder = new WorkItemBuilder(Project.Id).WithType("action").WithTitle($"Fix {Guid.NewGuid():N}");
            if (plannedIn is { } planned)
            {
                builder = builder.WithSprint(Sprints[planned]);
            }
            if (owner is { } user)
            {
                builder = builder.WithAssignee(user);
            }

            var id = await RetroHistory.CreateAsync(Connection, builder);
            await Seed.ActionAsync(new RetroSeedAction(id, theme, RaisedSprintId: Sprints[raisedIn]));
            return id;
        }
    }
}
