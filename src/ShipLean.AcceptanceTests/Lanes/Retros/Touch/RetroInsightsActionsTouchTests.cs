using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Retros.Touch;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// Retrospectives › Insights, Action items and Changes & impact on a phone (SHIP-731 D6 and D7, L731c): a card per action
// item (open first, then Done) that opens the item, the Pull button, the How verdicts work button and a tap on an impact
// bar. The touch rules are checked on this task's regions only (the cards and the impact cards).
[TestFixture]
public class RetroInsightsActionsTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    private static readonly Guid[] Mentioned = [SeedIds.Sprints.Sprint20, SeedIds.Sprints.Sprint21, SeedIds.Sprints.Sprint22, SeedIds.Sprints.Sprint23];

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task ActionItems_WhenOpenedOnAPhone_ShouldListCardsOpenFirstThenDone()
    {
        var arranged = await ArrangeAsync();
        var (insights, touch) = await OpenAsync("actions");

        await Expect(touch.CardOf(arranged.DoneTitle)).ToBeVisibleAsync();
        await Expect(insights.ActionsTable).ToHaveCountAsync(0);

        // The class shares one database, so other cards may be listed: the open one is above the Done one, and no Done card is above it.
        var texts = await touch.Card.AllInnerTextsAsync();
        var openAt = texts.ToList().FindIndex(t => t.Contains(arranged.OpenTitle, StringComparison.Ordinal));
        var doneAt = texts.ToList().FindIndex(t => t.Contains(arranged.DoneTitle, StringComparison.Ordinal));
        openAt.ShouldBeGreaterThanOrEqualTo(0);
        openAt.ShouldBeLessThan(doneAt);
        texts.Take(openAt).ShouldAllBe(t => !t.Contains("Closed", StringComparison.Ordinal));

        var open = touch.CardOf(arranged.OpenTitle);
        await Expect(open).ToContainTextAsync(arranged.OpenKey);
        await Expect(open).ToContainTextAsync("New");
        await Expect(open).ToContainTextAsync(arranged.ThemeName);
        var done = touch.CardOf(arranged.DoneTitle);
        await Expect(done).ToContainTextAsync(arranged.DoneKey);
        await Expect(done).ToContainTextAsync("Closed");
        await Expect(done).ToContainTextAsync(arranged.ThemeName);
        await Expect(done).ToContainTextAsync("Joel");

        (await TouchRules.ViolationsAsync(Page, touch.Cards)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, insights.ActionsAdd)).ShouldBeEmpty();
        var pull = RetroActionsTouchPart.PullOf(open);
        (await pull.BoundingBoxAsync())!.Height.ShouldBeGreaterThanOrEqualTo(32);
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();

        await pull.TapAsync();
        await Expect(RetroActionsTouchPart.PullOf(open)).ToHaveCountAsync(0);
        await Expect(open).ToContainTextAsync("Sprint");
    }

    [Test]
    public async Task ActionItemCard_WhenTapped_ShouldOpenTheItem()
    {
        var arranged = await ArrangeAsync();
        var (_, touch) = await OpenAsync("actions");

        await RetroActionsTouchPart.LinkOf(touch.CardOf(arranged.DoneTitle)).TapAsync();

        var peek = new ItemPeekDialog(Page);
        await peek.WaitOpenAsync(arranged.DoneKey);
        await Expect(peek.Dialog).ToBeVisibleAsync();
    }

    [Test]
    public async Task Impact_WhenABarIsTapped_ShouldReadOutItsColumnAndCount()
    {
        var arranged = await ArrangeAsync();
        var (insights, _) = await OpenAsync("impact");

        var card = insights.ImpactCardOf(arranged.DoneTitle);
        await Expect(card).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, insights.ImpactCards.First)).ShouldBeEmpty();
        await Expect(RetroActionsTouchPart.ReadoutOf(card)).ToHaveCountAsync(0);

        // The third column's bar: its count is two mentions a sprint at most.
        var bars = RetroActionsTouchPart.BarsOf(card);
        var sprint22 = bars.Nth(2);
        await sprint22.TapAsync();
        await Expect(RetroActionsTouchPart.ReadoutOf(card)).ToHaveTextAsync(new Regex(@"^S\d+: \d+ mentions?$"));
        await Expect(sprint22).ToHaveAttributeAsync("data-selected", string.Empty);
        await sprint22.TapAsync();
        await Expect(RetroActionsTouchPart.ReadoutOf(card)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task HowVerdictsWork_WhenTapped_ShouldWriteSheetDefs()
    {
        await ArrangeAsync();
        var (insights, touch) = await OpenAsync("impact");

        await Expect(insights.ImpactCards.First).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, touch.HowVerdicts)).ShouldBeEmpty();
        await touch.HowVerdicts.TapAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("sheet=defs"));
        Page.Url.ShouldContain("view=impact");
    }

    private async Task<(RetroInsightsPage Insights, RetroActionsTouchPart Touch)> OpenAsync(string view)
    {
        var insights = new RetroInsightsPage(Page);
        await insights.OpenAsync(Project, view);
        return (insights, new RetroActionsTouchPart(Page));
    }

    // A theme mentioned in four ended sprints, one open action item with no sprint and one that closed in Sprint 22 (so it has
    // an impact card).
    private async Task<Arranged> ArrangeAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var themeName = $"Flaky pipeline stage {Guid.NewGuid():N}";
        var theme = await seed.ThemeAsync(themeName);
        foreach (var sprint in Mentioned)
        {
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: sprint));
            for (var i = 0; i < 2; i++)
            {
                await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"The stage flaked again ({sprint.ToString()[^2..]}-{i})", ThemeId: theme, CreatedAt: await NoonInAsync(connection, sprint)));
            }
        }

        var openTitle = $"Retry the flaky stage {Guid.NewGuid():N}";
        var doneTitle = $"Quarantine the flaky stage {Guid.NewGuid():N}";
        // The closed item is created first so that the data lists it ahead of the open one: only the view puts the open card on top.
        var done = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(doneTitle).WithAssignee(SeedIds.Users.Joel));
        await seed.ActionAsync(new RetroSeedAction(done, theme, RaisedSprintId: SeedIds.Sprints.Sprint21));
        await RetroHistory.CompleteAsync(connection, done, await NoonInAsync(connection, SeedIds.Sprints.Sprint22));
        var open = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(openTitle));
        await seed.ActionAsync(new RetroSeedAction(open, theme, RaisedSprintId: SeedIds.Sprints.Sprint21));
        return new Arranged(themeName, openTitle, await KeyOfAsync(connection, open), doneTitle, await KeyOfAsync(connection, done));
    }

    private static async Task<string> KeyOfAsync(NpgsqlConnection connection, Guid id) =>
        await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException("no key");

    // Noon UTC on the fourth day of the sprint (sprint dates are computed from the run date).
    private static async Task<DateTimeOffset> NoonInAsync(NpgsqlConnection connection, Guid sprint)
    {
        var start = await connection.ExecuteScalarAsync<DateOnly>("SELECT start_date FROM sprint WHERE id = @sprint", new { sprint });
        var day = start.AddDays(3);
        return new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed record Arranged(string ThemeName, string OpenTitle, string OpenKey, string DoneTitle, string DoneKey);
}
