using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Retros.Touch;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// Recurring themes on a phone (390×844, SHIP-731 D1, D4, D9): a card per theme with a heat strip, the theme sheet in the
// address, Rename… with Discard and Save, and Merge into another theme… writing its parameter. The touch rules are
// checked on this task's regions only (the cards and the sheet). Every test seeds themes of its own, by name.
[TestFixture]
public class RetroInsightsThemesTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    private static readonly Guid[] Mentioned = [SeedIds.Sprints.Sprint20, SeedIds.Sprints.Sprint21, SeedIds.Sprints.Sprint22, SeedIds.Sprints.Sprint23];

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task ThemeCards_WhenOpenedOnAPhone_ShouldShowAHeatStripPerShownColumn()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var connection = await OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await NoSprintRetroAsync(connection, seed, $"Strip story {suffix}");
        var (first, second) = ($"Strip theme one {suffix}", $"Strip theme two {suffix}");
        await WentBadlyAsync(seed, retro, await seed.ThemeAsync(first), $"Strip point one {suffix}", DaysAgo(8));
        await WentBadlyAsync(seed, retro, await seed.ThemeAsync(second), $"Strip point two {suffix}", DaysAgo(15));
        var insights = new RetroInsightsPage(Page);
        var themes = new RetroThemesTouchPart(Page);

        await insights.OpenAsync(Project, range: "w12");

        await Expect(themes.CardOf(first)).ToHaveCountAsync(1);
        await Expect(RetroThemesTouchPart.HeatCellsOf(themes.CardOf(first))).ToHaveCountAsync(12);
        await Expect(RetroThemesTouchPart.HeatCellsOf(themes.CardOf(second))).ToHaveCountAsync(12);
        await Expect(RetroThemesTouchPart.HeatTotalOf(themes.CardOf(first))).ToHaveTextAsync("1");
        await Expect(insights.RecurringTable).ToHaveCountAsync(0);
        var labels = await RetroThemesTouchPart.HeatCellsOf(themes.CardOf(first)).EvaluateAllAsync<string[]>("cells => cells.map(c => c.getAttribute('title').split(':')[0])");
        await Expect(themes.Axis).ToContainTextAsync(labels[0]);
        await Expect(themes.Axis).ToContainTextAsync(labels[^1]);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        // The cards, one by one: the Unthemed row after them is the next task's.
        foreach (var card in await themes.CardList.AllAsync())
        {
            (await TouchRules.ViolationsAsync(Page, card)).ShouldBeEmpty();
        }

        // On a tablet two cards share a row.
        await Page.SetViewportSizeAsync(768, 1024);
        await Expect(themes.CardList.First).ToBeVisibleAsync();
        var boxes = new[] { await themes.CardList.Nth(0).BoundingBoxAsync(), await themes.CardList.Nth(1).BoundingBoxAsync() };
        Math.Abs((double)(boxes[0]!.Y - boxes[1]!.Y)).ShouldBeLessThan(2);
        ((double)(boxes[1]!.X - boxes[0]!.X)).ShouldBeGreaterThan(200);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
    }

    [Test]
    public async Task ThemeSheet_WhenACardIsTapped_ShouldShowEverythingAboutTheTheme()
    {
        const string actionTitle = "Quarantine the flaky stage for the sheet";
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var theme = $"Sheet theme {suffix}";
        await using var connection = await OpenAsync();
        var themeId = await ArrangeRecurringAsync(connection, theme, actionTitle);
        var insights = new RetroInsightsPage(Page);
        var themes = new RetroThemesTouchPart(Page);

        await insights.OpenAsync(Project);
        await themes.CardOf(theme).GetByTestId(RetrosTestIds.RetroHeatStrip).TapAsync();

        await Expect(themes.Sheet).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"[?&]sheet=theme(%3A|:){themeId}"));
        await Expect(themes.SheetTitle).ToHaveTextAsync(theme);
        // Four sprints with two mentions each; every other shown column has none.
        var counts = await themes.SheetCounts.EvaluateAllAsync<int[]>("cells => cells.map(c => Number(c.getAttribute('data-count')))");
        counts.Count(count => count == 2).ShouldBe(4);
        counts.Where(count => count != 2).ShouldAllBe(count => count == 0);
        var pairs = await themes.SheetCountPairsAsync();
        foreach (var sprint in Mentioned)
        {
            pairs.ShouldContain($"{sprint}=2");
        }

        await Expect(themes.SheetNoSprint).ToContainTextAsync("No sprint 0");
        await Expect(themes.SheetTotal).ToContainTextAsync("Total 8");
        await Expect(themes.SheetActions).ToHaveCountAsync(1);
        await Expect(themes.SheetActions).ToContainTextAsync(actionTitle);
        await Expect(themes.SheetAddAction).ToContainTextAsync("+ Action item on 8 open points");
        await Expect(themes.SheetMentions).ToHaveCountAsync(3);
        await Expect(themes.AllInSearch).ToHaveTextAsync("All 8 in Search");
        (await TouchRules.ViolationsAsync(Page, themes.Sheet)).ShouldBeEmpty();

        await themes.AllInSearch.TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/retros\\?theme={themeId}$"));
        await new RetrosSearchPage(Page).Panel.WaitForAsync();
    }

    [Test]
    public async Task Rename_WhenSavedInTheSheet_ShouldRenameTheThemeEverywhere()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (theme, renamed) = ($"Rename theme {suffix}", $"Renamed theme {suffix}");
        await using var connection = await OpenAsync();
        await ArrangeRecurringAsync(connection, theme, $"Rename action {suffix}");
        var insights = new RetroInsightsPage(Page);
        var themes = new RetroThemesTouchPart(Page);

        await insights.OpenAsync(Project);
        await themes.OpenOf(theme).TapAsync();
        await themes.RenameOpen.TapAsync();
        (await themes.RenameField.EvaluateAsync<string>("e => getComputedStyle(e).fontSize")).ShouldBe("16px");
        await Expect(themes.RenameField).ToHaveValueAsync(theme);
        await Expect(themes.RenameSave).ToBeDisabledAsync();
        await themes.RenameField.FillAsync(renamed);
        (await TouchRules.ViolationsAsync(Page, themes.Sheet)).ShouldBeEmpty();
        await themes.RenameSave.TapAsync();

        await Expect(themes.RenameField).ToHaveCountAsync(0);
        await Expect(themes.SheetTitle).ToHaveTextAsync(renamed);
        await themes.SheetClose.TapAsync();
        await Expect(themes.CardOf(renamed)).ToHaveCountAsync(1);
        await Expect(themes.CardOf(theme)).ToHaveCountAsync(0);

        await Page.SetViewportSizeAsync(1280, 900);
        await Expect(insights.RecurringRowOf(renamed)).ToHaveCountAsync(1);
        await Expect(insights.RecurringRowOf(theme)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Rename_WhenDiscarded_ShouldKeepTheName()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var theme = $"Keep theme {suffix}";
        await using var connection = await OpenAsync();
        await ArrangeRecurringAsync(connection, theme, $"Keep action {suffix}");
        var insights = new RetroInsightsPage(Page);
        var themes = new RetroThemesTouchPart(Page);

        await insights.OpenAsync(Project);
        await themes.OpenOf(theme).TapAsync();
        await themes.RenameOpen.TapAsync();
        await themes.RenameField.FillAsync($"Not kept {suffix}");
        await themes.RenameDiscard.TapAsync();

        await Expect(themes.RenameField).ToHaveCountAsync(0);
        await Expect(themes.SheetTitle).ToHaveTextAsync(theme);
        await themes.SheetClose.TapAsync();
        await Expect(themes.CardOf(theme)).ToHaveCountAsync(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM retro_theme WHERE name = @name", new { name = $"Not kept {suffix}" })).ShouldBe(0);
    }

    [Test]
    public async Task ThemeSheet_WhenMergeIntoTapped_ShouldWriteMergeWithTheTheme()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var theme = $"Merge theme {suffix}";
        await using var connection = await OpenAsync();
        var themeId = await ArrangeRecurringAsync(connection, theme, $"Merge action {suffix}");
        var insights = new RetroInsightsPage(Page);
        var themes = new RetroThemesTouchPart(Page);

        await insights.OpenAsync(Project);
        await themes.OpenOf(theme).TapAsync();
        await themes.MergeInto.TapAsync();

        // The merge view is the next task's; this one writes the address and leaves the sheet.
        await Expect(Page).ToHaveURLAsync(new Regex($"[?&]merge={themeId}($|&)"));
        Page.Url.ShouldNotContain("sheet=");
        await Expect(themes.Sheet).ToHaveCountAsync(0);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static DateTimeOffset DaysAgo(int days) => TimeProvider.System.GetUtcNow().AddDays(-days);

    // A retro on a story that has no sprint, so its points belong to no sprint.
    private static async Task<Guid> NoSprintRetroAsync(NpgsqlConnection connection, RetroSeed seed, string storyTitle)
    {
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(storyTitle).InsertAsync(connection);
        return await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, WorkItemId: story));
    }

    private static Task<Guid> WentBadlyAsync(RetroSeed seed, Guid retro, Guid theme, string text, DateTimeOffset at)
        => seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, text, ThemeId: theme, CreatedAt: at));

    // A theme with two Went badly points in each of Sprints 20-23 (eight in all, every one undecided) and an open action item.
    private static async Task<Guid> ArrangeRecurringAsync(NpgsqlConnection connection, string themeName, string actionTitle)
    {
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var theme = await seed.ThemeAsync(themeName);
        foreach (var sprint in Mentioned)
        {
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: sprint));
            for (var i = 0; i < 2; i++)
            {
                await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"{themeName} point {sprint.ToString()[^2..]}-{i}", ThemeId: theme, CreatedAt: await NoonInAsync(connection, sprint)));
            }
        }

        var action = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(actionTitle));
        await seed.ActionAsync(new RetroSeedAction(action, theme, RaisedSprintId: SeedIds.Sprints.Sprint21));
        return theme;
    }

    // Noon UTC on the fourth day of the sprint (sprint dates are computed from the run date).
    private static async Task<DateTimeOffset> NoonInAsync(NpgsqlConnection connection, Guid sprint)
    {
        var start = await connection.ExecuteScalarAsync<DateOnly>("SELECT start_date FROM sprint WHERE id = @sprint", new { sprint });
        var day = start.AddDays(3);
        return new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero);
    }
}
