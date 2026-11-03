using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Insights;

// Insights on the real reader: week mode (dates, Recurring over 6 weeks), No sprint points, the Unthemed row's link, a
// theme row's + Action item, merge proposals and the Action items view. Every test arranges themes of its own by name,
// through SQL (RetroSeed), against dates counted back from today, so the tests do not depend on one another.
[TestFixture]
public class RetroWeeksJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    [Test]
    public async Task Weeks_WhenSelected_ShouldShowDatesAndNoNoSprintColumn()
    {
        const string theme = "Weeks columns theme";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, "Weeks columns story");
        await WentBadlyAsync(seed, retro, await seed.ThemeAsync(theme), "Columns point", DaysAgo(7));
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project, range: "w6");

        await Expect(insights.MentionCellsOf(theme)).ToHaveCountAsync(6);
        await Expect(insights.NoSprintHead).ToHaveCountAsync(0);
        await Expect(insights.NoSprintOf(theme)).ToHaveCountAsync(0);
        var keys = await insights.MentionCellsOf(theme).EvaluateAllAsync<string[]>("cells => cells.map(c => c.getAttribute('data-sprint-id'))");
        keys.ShouldAllBe(key => IsMonday(key));
        keys[^1].ShouldBe(MondayOf(Today()).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var titles = await insights.MentionCellsOf(theme).EvaluateAllAsync<string[]>("cells => cells.map(c => c.getAttribute('title'))");
        titles.ShouldNotContain(title => title.Contains("Sprint"));

        await insights.OpenAsync(Project);
        await Expect(insights.NoSprintHead).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Weeks_WhenThreeOfSix_ShouldShowRecurringWithWeekWording()
    {
        const string three = "Three of six weeks";
        const string two = "Two of six weeks";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, "Three of six story");
        var threeTheme = await seed.ThemeAsync(three);
        var twoTheme = await seed.ThemeAsync(two);
        foreach (var days in new[] { 7, 14, 21 })
        {
            await WentBadlyAsync(seed, retro, threeTheme, $"{three} {days}", DaysAgo(days));
        }

        foreach (var days in new[] { 7, 14 })
        {
            await WentBadlyAsync(seed, retro, twoTheme, $"{two} {days}", DaysAgo(days));
        }

        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project, range: "w6");

        await Expect(insights.RecurringRowOf(three)).ToHaveAttributeAsync("data-status", "recurring");
        await Expect(insights.RecurringRowOf(two)).Not.ToHaveAttributeAsync("data-status", "recurring");
        await Expect(insights.Callout).ToContainTextAsync("last 6 weeks");
        await insights.StatusOf(three).HoverAsync();
        await Expect(insights.Tooltip).ToContainTextAsync("3 of the last 6 weeks");
    }

    [Test]
    public async Task Sprints_WhenNoSprintPoints_ShouldCountInTotalNotRecurring()
    {
        const string theme = "No sprint only theme";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, "No sprint story");
        var themeId = await seed.ThemeAsync(theme);
        foreach (var days in new[] { 7, 14, 21 })
        {
            await WentBadlyAsync(seed, retro, themeId, $"{theme} {days}", DaysAgo(days));
        }

        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project);

        await Expect(insights.NoSprintOf(theme)).ToHaveAttributeAsync("data-count", "3");
        await Expect(insights.TotalOf(theme)).ToHaveTextAsync("3");
        await Expect(insights.RecurringRowOf(theme)).Not.ToHaveAttributeAsync("data-status", "recurring");
        foreach (var cell in await insights.MentionCellsOf(theme).AllAsync())
        {
            await Expect(cell).ToHaveAttributeAsync("data-count", "0");
        }
    }

    [Test]
    public async Task Unthemed_WhenLinkFollowed_ShouldOpenSearchFiltered()
    {
        const string text = "Nobody gave this point a theme";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, "Unthemed story");
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, text, CreatedAt: DaysAgo(3)));
        var insights = new RetroInsightsPage(Page);
        var search = new RetrosSearchPage(Page);

        await insights.OpenAsync(Project);
        await Expect(insights.UnthemedRow).ToBeVisibleAsync();
        await insights.GiveThemes.ClickAsync();

        await Expect(search.Panel).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("unthemed=true"));
        await Expect(search.FilterUnthemed).ToBeCheckedAsync();
        await Expect(search.RowOf(text)).ToBeVisibleAsync();
    }

    [Test]
    public async Task ThemeRowAction_ShouldOpenTheDialogWithThemeUndecidedPicked()
    {
        const string theme = "Dialog theme";
        const string title = "Fix the dialog theme";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, "Dialog story");
        var themeId = await seed.ThemeAsync(theme);
        await WentBadlyAsync(seed, retro, themeId, "Dialog point one", DaysAgo(2));
        await WentBadlyAsync(seed, retro, themeId, "Dialog point two", DaysAgo(3));
        var insights = new RetroInsightsPage(Page);
        var dialog = new RetroActionDialogPart(Page);

        await insights.OpenAsync(Project);
        await insights.ThemeAction(theme).ClickAsync();

        await Expect(dialog.Dialog).ToBeVisibleAsync();
        await Expect(dialog.Pick.Filter(new() { HasText = "Dialog point one" }).GetByTestId(RetroSharedTestIds.RetroActionPickCheck)).ToBeCheckedAsync();
        await Expect(dialog.Pick.Filter(new() { HasText = "Dialog point two" }).GetByTestId(RetroSharedTestIds.RetroActionPickCheck)).ToBeCheckedAsync();
        await Expect(dialog.Theme).ToHaveValueAsync(themeId.ToString());

        await dialog.Title.FillAsync(title);
        await dialog.FixOption("Test").ClickAsync();
        await dialog.Save.ClickAsync();

        await Expect(dialog.Dialog).ToHaveCountAsync(0);
        await insights.OpenAsync(Project, view: "actions");
        await Expect(insights.ActionRowOf(title)).ToBeVisibleAsync();
        await Expect(insights.CoversOf(title)).ToHaveTextAsync("2");
        await Expect(insights.FixOf(title)).ToHaveTextAsync("Test");
    }

    [Test]
    public async Task Proposal_WhenKeptApart_ShouldNotComeBack()
    {
        const string from = "Keep apart from";
        const string into = "Keep apart into";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var proposal = await ProposeAsync(connection, seed, from, into);
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project);
        await Expect(insights.ProposalOf(from)).ToBeVisibleAsync();
        await insights.ProposalKeep(from).ClickAsync();

        await Expect(insights.ProposalOf(from)).ToHaveCountAsync(0);
        await insights.OpenAsync(Project);
        await Expect(insights.RecurringRowOf(from)).ToBeVisibleAsync();
        await Expect(insights.RecurringRowOf(into)).ToBeVisibleAsync();
        await Expect(insights.ProposalOf(from)).ToHaveCountAsync(0);
        (await connection.QuerySingleAsync<string>("SELECT status FROM retro_theme_merge_proposal WHERE id = @proposal", new { proposal })).ShouldBe("keptApart");
    }

    [Test]
    public async Task Proposal_WhenMerged_ShouldMovePoints()
    {
        const string from = "Merge from";
        const string into = "Merge into";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var proposal = await ProposeAsync(connection, seed, from, into);
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project, view: "actions");
        await Expect(insights.Proposals).ToHaveCountAsync(0);
        await insights.OpenAsync(Project);
        await Expect(insights.TotalOf(into)).ToHaveTextAsync("1");
        await Expect(insights.ProposalOf(from)).ToContainTextAsync("2 points");
        await insights.ProposalMerge(from).ClickAsync();

        await Expect(insights.ProposalOf(from)).ToHaveCountAsync(0);
        await Expect(insights.RecurringRowOf(from)).ToHaveCountAsync(0);
        await Expect(insights.TotalOf(into)).ToHaveTextAsync("3");
        (await connection.QuerySingleAsync<string>("SELECT status FROM retro_theme_merge_proposal WHERE id = @proposal", new { proposal })).ShouldBe("merged");
    }

    [Test]
    public async Task ActionItemsView_ShouldShowFixAndCovers()
    {
        const string theme = "Action view theme";
        const string title = "Add the analyzer for the action view";
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, "Action view story");
        var themeId = await seed.ThemeAsync(theme);
        var one = await WentBadlyAsync(seed, retro, themeId, "Action view one", DaysAgo(2));
        var two = await WentBadlyAsync(seed, retro, themeId, "Action view two", DaysAgo(3));
        var three = await WentBadlyAsync(seed, retro, themeId, "Action view three", DaysAgo(4));
        var action = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(title));
        await seed.ActionAsync(new RetroSeedAction(action, themeId, RaisedSprintId: SeedIds.Sprints.Sprint21, FixType: RetroFixType.Analyzer, PointIds: [one, two, three]));
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project, view: "actions");

        await Expect(insights.ActionRowOf(title)).ToBeVisibleAsync();
        await Expect(insights.FixOf(title)).ToHaveTextAsync("Analyzer");
        await Expect(insights.CoversOf(title)).ToHaveTextAsync("3");
    }

    [TestCase(1280)]
    [TestCase(1440)]
    public async Task Insights_WhenTallerThanTheWindow_ShouldKeepItsBarInViewWithoutSidewaysScroll(int width)
    {
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, $"Layout story {width}");
        for (var i = 0; i < 30; i++)
        {
            await WentBadlyAsync(seed, retro, await seed.ThemeAsync($"Layout theme {width} {i}"), $"Layout point {width} {i}", DaysAgo(i + 1));
        }

        await Page.SetViewportSizeAsync(width, 720);
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project, range: "w12");
        await Page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");

        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth")).ShouldBeTrue();
        var bar = await insights.Range.BoundingBoxAsync();
        bar.ShouldNotBeNull();
        (bar.Y >= 0 && bar.Y + bar.Height <= 720).ShouldBeTrue($"the range select sits at y={bar.Y}");
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static DateTimeOffset DaysAgo(int days) => TimeProvider.System.GetUtcNow().AddDays(-days);

    private static DateOnly Today() => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private static bool IsMonday(string key) => DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day.DayOfWeek == DayOfWeek.Monday;

    private static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    // A retro on a story that has no sprint, so its points belong to no sprint.
    private static async Task<Guid> NoSprintRetroAsync(NpgsqlConnection connection, RetroSeed seed, string storyTitle)
    {
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(storyTitle).InsertAsync(connection);
        return await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: story));
    }

    private static Task<Guid> WentBadlyAsync(RetroSeed seed, Guid retro, Guid theme, string text, DateTimeOffset at)
        => seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, text, ThemeId: theme, CreatedAt: at));

    // A pending proposal to merge `from` (two points) into `into` (one point), left by an agent.
    private static async Task<Guid> ProposeAsync(NpgsqlConnection connection, RetroSeed seed, string from, string into)
    {
        var retro = await NoSprintRetroAsync(connection, seed, $"{from} story");
        var fromTheme = await seed.ThemeAsync(from);
        var intoTheme = await seed.ThemeAsync(into);
        await WentBadlyAsync(seed, retro, fromTheme, $"{from} one", DaysAgo(2));
        await WentBadlyAsync(seed, retro, fromTheme, $"{from} two", DaysAgo(3));
        await WentBadlyAsync(seed, retro, intoTheme, $"{into} one", DaysAgo(4));
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO retro_theme_merge_proposal (id, project_id, from_theme_id, into_theme_id, reason, proposed_by_user_id, proposed_agent_name) VALUES (@id, @project, @fromTheme, @intoTheme, 'same cause', @user, 'claude-x')",
            new { id, project = SeedIds.Projects.Ship, fromTheme, intoTheme, user = SeedIds.Users.Joel });
        return id;
    }
}
