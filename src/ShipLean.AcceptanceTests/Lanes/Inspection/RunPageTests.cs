using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Inspection;

// The run page for the synthetic Claude Code run, served by the FakeTraceSource behind the real route and SPA.
// It is opened by its address: the Agent runs section on the item page (SHIP-1565) is built alongside and is not required here.
[TestFixture]
public class RunPageTests : ShipLean.AcceptanceTests.Inspection.InspectionAcceptanceTest
{
    private const string RunId = "sess-cc-1";

    private static string RunPath => $"/p/{ProjectKey}/items/{ItemKey}/runs/{RunId}";

    private async Task OpenRunAsync()
    {
        await Page.GotoAsync(BaseUrl + RunPath);
        await Expect(Page.GetByTestId(InspectionTestIds.RunHeader)).ToBeVisibleAsync();
    }

    [Test]
    public async Task RunPage_WhenOpened_ShouldShowTheRunsHeaderTotalsAndTurns()
    {
        await OpenRunAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.RunAgent)).ToHaveTextAsync("Claude Code");
        await Expect(Page.GetByTestId(InspectionTestIds.RunTotalTook)).ToHaveTextAsync("1 m 25 s");
        await Expect(Page.GetByTestId(InspectionTestIds.RunTotalTokens)).ToHaveTextAsync("7,000 / 1,470");
        await Expect(Page.GetByTestId(InspectionTestIds.RunTotalErrors)).ToHaveTextAsync("4");
        await Expect(Page.GetByTestId(InspectionTestIds.RunTurn)).ToHaveCountAsync(2);
    }

    [Test]
    public async Task RunPage_WhenTurnClicked_ShouldZoomTheAxisToItAndWholeRunResets()
    {
        await OpenRunAsync();
        var axis = Page.GetByTestId(InspectionTestIds.RunAxis);
        var whole = await axis.InnerTextAsync();
        await Page.GetByTestId(InspectionTestIds.RunOverviewTurn).First.ClickAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.RunOverview)).ToHaveAttributeAsync("data-zoom-turn", "1");
        await Expect(Page.GetByTestId(InspectionTestIds.RunTurn)).ToHaveCountAsync(1);
        var zoomed = await axis.InnerTextAsync();
        zoomed.ShouldNotBe(whole);
        await Page.GetByTestId(InspectionTestIds.RunWholeRun).ClickAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.RunTurn)).ToHaveCountAsync(2);
    }

    [Test]
    public async Task RunPage_WhenErrorsOnlyOn_ShouldKeepOnlyFailingRows()
    {
        await OpenRunAsync();
        var rows = Page.GetByTestId(InspectionTestIds.RunRow);
        var all = await rows.CountAsync();
        await Page.GetByTestId(InspectionTestIds.RunErrorsOnly).ClickAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.RunErrorsOnly)).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(rows).Not.ToHaveCountAsync(all);
        var errors = await Page.GetByTestId(InspectionTestIds.RunRowError).CountAsync();
        errors.ShouldBeGreaterThan(0);
        await Expect(rows).ToHaveCountAsync(errors);
    }

    [Test]
    public async Task RunPage_WhenBackLinkClicked_ShouldReturnToTheItem()
    {
        await OpenRunAsync();
        await Page.GetByTestId(InspectionTestIds.RunBack).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/p/{ProjectKey}/items/{ItemKey}$"));
    }
}
