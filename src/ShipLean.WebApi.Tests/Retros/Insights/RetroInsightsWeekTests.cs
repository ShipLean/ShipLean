using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Insights;

// Insights in week mode, with the clock pinned to Wednesday 18 March 2026 (UTC), so this week is Monday 16 March. The six weeks shown
// start on Monday 9 February: 9 Feb, 16 Feb, 23 Feb, 2 Mar, 9 Mar, 16 Mar. The projects have no sprints at all.
public sealed class RetroInsightsWeekTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    private IRetroInsightsReader Reader => Host.Services.GetRequiredService<IRetroInsightsReader>();

    [Test]
    public async Task Get_WhenW6_ShouldReturnSixWeekColumnsWithMondayLabels()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection);

        var result = await Reader.GetAsync(project.Id, RetroRange.Weeks6, CancellationToken.None);
        var wide = await Reader.GetAsync(project.Id, RetroRange.Weeks12, CancellationToken.None);

        result.Range.ShouldBe("w6");
        result.Mode.ShouldBe(RetroInsightsMode.Week);
        result.Sprints.ShouldBeEmpty();
        result.Columns.Select(column => column.Key).ShouldBe(["2026-02-09", "2026-02-16", "2026-02-23", "2026-03-02", "2026-03-09", "2026-03-16"]);
        result.Columns.Select(column => column.Label).ShouldBe(["9 Feb", "16 Feb", "23 Feb", "2 Mar", "9 Mar", "16 Mar"]);
        result.Columns.ShouldAllBe(column => column.Kind == RetroInsightColumnKind.Week);
        result.Columns.Select(column => column.IsCurrent).ShouldBe([false, false, false, false, false, true]);
        result.Unthemed.Counts.ShouldBe([0, 0, 0, 0, 0, 0]);
        wide.Range.ShouldBe("w12");
        wide.Columns.Count.ShouldBe(12);
        wide.Columns[0].Key.ShouldBe("2025-12-29");
        wide.Columns[0].Label.ShouldBe("29 Dec 2025");
    }

    [Test]
    public async Task Get_WhenPointHasNoSprint_ShouldPlaceItByDayLogged()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        var sprintStart = new DateOnly(2026, 3, 2);
        var sprint = await RetroArrangement.SprintAsync(connection, world.Project.Id, "S1", sprintStart, sprintStart.AddDays(6));
        var noSprint = await world.NoSprintRetroAsync(connection);
        var inSprint = await world.Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: sprint));
        await world.Seed.PointAsync(noSprint, new RetroSeedPoint(RetroCategory.WentBadly, "Logged with no sprint.", ThemeId: theme, CreatedAt: At(2026, 3, 10, 9)));
        await world.Seed.PointAsync(inSprint, new RetroSeedPoint(RetroCategory.WentBadly, "Logged after its sprint ended.", ThemeId: theme, CreatedAt: At(2026, 3, 11, 9)));
        await world.Seed.PointAsync(inSprint, new RetroSeedPoint(RetroCategory.WentBadly, "Logged inside its sprint.", ThemeId: theme, CreatedAt: At(2026, 3, 3, 9)));

        var row = (await Reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None)).Themes.Single();

        row.Counts.ShouldBe([0, 0, 0, 1, 2, 0]);
        row.Total.ShouldBe(3);
        row.NoSprint.ShouldBe(0);
    }

    [Test]
    public async Task Get_WhenSundayLateAndMondayEarly_ShouldSplitWeeks()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        var retro = await world.NoSprintRetroAsync(connection);
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Sunday night.", ThemeId: theme, CreatedAt: new DateTimeOffset(2026, 3, 8, 23, 59, 59, TimeSpan.Zero)));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Monday morning.", ThemeId: theme, CreatedAt: new DateTimeOffset(2026, 3, 9, 0, 0, 0, TimeSpan.Zero)));

        var row = (await Reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None)).Themes.Single();

        row.Counts.ShouldBe([0, 0, 0, 1, 1, 0]);
        row.Latest.ShouldBe("Monday morning.");
    }

    [Test]
    public async Task Get_WhenWeekMode_ShouldJudgeVerdictsOnWeeks()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(connection, theme, At(2026, 2, 17, 9), 2);
        await world.MentionAsync(connection, theme, At(2026, 2, 24, 9), 2);
        await world.MentionAsync(connection, theme, At(2026, 3, 3, 9), 2);
        await world.MentionAsync(connection, theme, At(2026, 3, 17, 9), 5);
        var action = await world.ActionAsync(connection, theme);
        await RetroHistory.CompleteAsync(connection, action, At(2026, 3, 4, 12));

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None);

        var impact = result.Impact.Single();
        impact.Counts.ShouldBe([0, 2, 2, 2, 0, 5]);
        impact.ClosedAt.ShouldBe(3);
        impact.BeforeAvg.ShouldBe(1.5d);
        impact.AfterAvg.ShouldBe(0d);
        impact.Verdict.ShouldBe(RetroVerdict.Helped);
        result.Themes.Single().Status.ShouldBe(RetroThemeStatus.Resolved);
        result.Themes.Single().LastVerdict.ShouldBe(RetroVerdict.Helped);
    }

    [Test]
    public async Task Get_WhenWeekMode_ShouldRecurOnThreeOfSixWeeks()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var spread = await world.Seed.ThemeAsync("Spread out");
        var twice = await world.Seed.ThemeAsync("Only twice");
        foreach (var day in new[] { At(2026, 2, 10, 9), At(2026, 2, 24, 9), At(2026, 3, 10, 9) })
        {
            await world.MentionAsync(connection, spread, day, 1);
        }
        foreach (var day in new[] { At(2026, 2, 10, 9), At(2026, 3, 10, 9) })
        {
            await world.MentionAsync(connection, twice, day, 1);
        }

        var themes = (await Reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None)).Themes;

        themes.Single(row => row.Theme.Id == spread).Status.ShouldBe(RetroThemeStatus.Recurring);
        themes.Single(row => row.Theme.Id == twice).Status.ShouldBe(RetroThemeStatus.Quiet);
    }

    [Test]
    public async Task Get_WhenCurrentWeek_ShouldNotBeJudged()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(connection, theme, At(2026, 3, 3, 9), 3);
        var action = await world.ActionAsync(connection, theme);
        await RetroHistory.CompleteAsync(connection, action, At(2026, 3, 10, 12));

        var impact = (await Reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None)).Impact.Single();

        impact.Counts.ShouldBe([0, 0, 0, 3, 0, 0]);
        impact.ClosedAt.ShouldBe(4);
        impact.Verdict.ShouldBe(RetroVerdict.TooEarly);
        impact.AfterAvg.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenWeekMode_ShouldHaveZeroNoSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var world = await WorldAsync(connection);
        var theme = await world.Seed.ThemeAsync("Slow reviews");
        await world.MentionAsync(connection, theme, At(2026, 3, 10, 9), 2);
        var retro = await world.NoSprintRetroAsync(connection);
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "No theme, no sprint.", CreatedAt: At(2026, 3, 11, 9)));

        var result = await Reader.GetAsync(world.Project.Id, RetroRange.Weeks6, CancellationToken.None);

        var row = result.Themes.Single();
        row.NoSprint.ShouldBe(0);
        row.Total.ShouldBe(2);
        result.Unthemed.NoSprint.ShouldBe(0);
        result.Unthemed.Counts.ShouldBe([0, 0, 0, 0, 1, 0]);
        result.Unthemed.Total.ShouldBe(1);
    }

    private static DateTimeOffset At(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    private static async Task<World> WorldAsync(NpgsqlConnection connection)
    {
        var project = await ArrangedProject.CreateAsync(connection);
        return new World(project, new RetroSeed(connection, project.Id));
    }

    private sealed record World(ArrangedProject Project, RetroSeed Seed)
    {
        // A retro on a story, with no sprint: a retro needs one or the other.
        public async Task<Guid> NoSprintRetroAsync(NpgsqlConnection connection)
        {
            var story = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(Project.Id).WithType("story").WithTitle($"Story {Guid.NewGuid():N}"));
            return await Seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: story));
        }

        public async Task MentionAsync(NpgsqlConnection connection, Guid theme, DateTimeOffset at, int count)
        {
            var retro = await NoSprintRetroAsync(connection);
            for (var i = 0; i < count; i++)
            {
                await Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Point {at:MM-dd}-{i}", ThemeId: theme, CreatedAt: at.AddMinutes(i)));
            }
        }

        public async Task<Guid> ActionAsync(NpgsqlConnection connection, Guid theme)
        {
            var id = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(Project.Id).WithType("action").WithTitle($"Fix {Guid.NewGuid():N}"));
            await Seed.ActionAsync(new RetroSeedAction(id, theme));
            return id;
        }
    }
}
