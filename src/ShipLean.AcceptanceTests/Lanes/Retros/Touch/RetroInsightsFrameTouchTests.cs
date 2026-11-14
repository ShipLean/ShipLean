using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Retros.Touch;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// Retrospective insights on a phone (SHIP-731, L731a): the frame, which is the three tabs, Range ▾ and ⋯ and the sheets
// they open. The touch rules are checked on the toolbar and the sheets only; the views under it are L731b-d's.
[TestFixture]
public class RetroInsightsFrameTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Tabs_WhenOpenedAt320_ShouldFitAndKeepTheViewInTheAddress()
    {
        await Page.SetViewportSizeAsync(320, 700);
        var (_, touch) = await OpenAsync();
        await Expect(touch.Toolbar).ToHaveAttributeAsync("data-loaded", "true");

        await Expect(touch.Tabs).ToHaveTextAsync(["Themes", "Action items", "Impact"]);
        var last = (await touch.Tabs.Nth(2).BoundingBoxAsync())!;
        (last.X + last.Width).ShouldBeLessThanOrEqualTo(320);
        foreach (var tab in await touch.Tabs.AllAsync())
        {
            (await tab.BoundingBoxAsync())!.Height.ShouldBe(40, 0.5);
        }

        (await TouchRules.ViolationsAsync(Page, touch.Toolbar)).ShouldBeEmpty();
        await touch.Tab("Impact").TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("view=impact"));
        await Expect(touch.Tab("Impact")).ToHaveAttributeAsync("aria-pressed", "true");
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
    }

    [Test]
    public async Task Range_WhenPickedInTheSheet_ShouldApplyAndClose()
    {
        var (_, touch) = await OpenAsync();

        await touch.RangeButton.TapAsync();
        await Expect(touch.Sheet).ToBeVisibleAsync();
        await Expect(touch.SheetTitle).ToHaveTextAsync("Range");
        await Expect(touch.RangeGroups).ToHaveCountAsync(2);
        await Expect(touch.RangeOptions).ToHaveCountAsync(5);
        (await TouchRules.ViolationsAsync(Page, touch.Sheet)).ShouldBeEmpty();

        await touch.RangeOption("Last 12 weeks").TapAsync();
        await Expect(touch.Sheet).ToHaveCountAsync(0);
        await Expect(Page).ToHaveURLAsync(new Regex("range=w12"));
        Page.Url.ShouldNotContain("sheet=");
        await Expect(touch.RangeButton).ToContainTextAsync("Last 12 weeks");
    }

    [Test]
    public async Task More_WhenOpened_ShouldListTheFiveActions()
    {
        var (_, touch) = await OpenAsync();

        await touch.More.TapAsync();
        await Expect(touch.MoreRows).ToHaveTextAsync(
        [
            new Regex("^Merge themes…$"),
            new Regex(@"^Give unthemed points themes \(\d+\)$"),
            new Regex("^What the statuses and verdicts mean$"),
            new Regex("^Search retrospectives$"),
            new Regex("^Retrospectives by Feature$"),
        ]);
        (await TouchRules.ViolationsAsync(Page, touch.Sheet)).ShouldBeEmpty();

        await touch.MoreRow("Retrospectives by Feature").TapAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/retros/features$"));

        await Page.GoBackAsync();
        await new RetroInsightsPage(Page).Panel.WaitForAsync();
        await touch.More.TapAsync();
        await touch.MoreRow("What the statuses and verdicts mean").TapAsync();
        await Expect(touch.SheetTitle).ToHaveTextAsync("What these mean");
        await Expect(Page).ToHaveURLAsync(new Regex("sheet=defs"));
        (await touch.HighlightedCountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task StatusChip_WhenTapped_ShouldOpenWhatTheseMeanAtThatEntry()
    {
        var theme = $"Phone frame theme {Guid.NewGuid():N}"[..40];
        await ArrangeRecurringThemeAsync(theme);
        var (insights, touch) = await OpenAsync();

        await new RetroThemesTouchPart(Page).CardOf(theme).GetByTestId(RetrosTestIds.RetroStatusChip).TapAsync();
        await Expect(touch.SheetTitle).ToHaveTextAsync("What these mean");
        (await touch.HighlightedCountAsync()).ShouldBe(1);
        await Expect(touch.DefsEntryOf("Recurring")).ToHaveAttributeAsync("data-highlighted", "true");
        await Expect(Page).ToHaveURLAsync(new Regex("sheet=defs%3Arecurring|sheet=defs:recurring"));
        await Expect(insights.Tooltip).ToHaveCountAsync(0);

        await touch.SheetClose.TapAsync();
        await Expect(touch.Sheet).ToHaveCountAsync(0);
        Page.Url.ShouldNotContain("sheet=");
    }

    [Test]
    public async Task Toolbar_WhenThePageScrolls_ShouldScrollWithIt()
    {
        await ArrangeRecurringThemeAsync($"Phone scroll theme {Guid.NewGuid():N}"[..40]);
        await Page.SetViewportSizeAsync(390, 300);
        var (_, touch) = await OpenAsync();
        await Expect(touch.Toolbar).ToHaveAttributeAsync("data-loaded", "true");

        var pinnedBottom = (await Page.GetByTestId(TouchTestIds.TopBar).BoundingBoxAsync())!.Y + (await Page.GetByTestId(TouchTestIds.TopBar).BoundingBoxAsync())!.Height;
        var before = (await touch.Toolbar.BoundingBoxAsync())!;

        // Scrolled well past where a sticky toolbar would have stuck under the top bar, it has moved by exactly as much as the page did.
        var scrolled = await Page.EvaluateAsync<double>("() => { window.scrollTo(0, document.documentElement.scrollHeight); return window.scrollY; }");
        scrolled.ShouldBeGreaterThan(before.Y - pinnedBottom + 1);
        var after = (await touch.Toolbar.BoundingBoxAsync())!;
        ((double)(before.Y - after.Y)).ShouldBe(scrolled, 1);
    }

    private async Task<(RetroInsightsPage Insights, RetroInsightsTouch Touch)> OpenAsync()
    {
        var insights = new RetroInsightsPage(Page);
        await insights.OpenAsync(Project);
        return (insights, new RetroInsightsTouch(Page));
    }

    // A theme mentioned in four ended sprints, so its status is Recurring (seeded as RetroInsightsJourneyTests does).
    private async Task ArrangeRecurringThemeAsync(string themeName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var theme = await seed.ThemeAsync(themeName);
        foreach (var sprint in new[] { SeedIds.Sprints.Sprint20, SeedIds.Sprints.Sprint21, SeedIds.Sprints.Sprint22, SeedIds.Sprints.Sprint23 })
        {
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: sprint));
            var start = await connection.ExecuteScalarAsync<DateOnly>("SELECT start_date FROM sprint WHERE id = @sprint", new { sprint });
            var day = start.AddDays(3);
            var at = new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero);
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"The frame flaked ({sprint.ToString()[^2..]})", ThemeId: theme, CreatedAt: at));
        }
    }
}
