using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Inspection;

// The run page for the synthetic Codex CLI run (log-only: nine events, no spans), through the real route and SPA.
[TestFixture]
public class LogOnlyRunPageTests : ShipLean.AcceptanceTests.Inspection.InspectionAcceptanceTest
{
    protected override IReadOnlyList<string> FixtureNames => ["codex-cli-synthetic"];

    private static string RunPath => $"/p/{ProjectKey}/items/{ItemKey}/runs/sess-cx-1";

    [Test]
    public async Task RunPage_WhenRunIsLogOnly_ShouldShowEventRowsDiamondsAndTheBanner()
    {
        await Page.GotoAsync(BaseUrl + RunPath);
        await Expect(Page.GetByTestId(InspectionTestIds.RunEvents)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(InspectionTestIds.RunWaterfall)).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(InspectionTestIds.RunRowEvent)).ToHaveCountAsync(9);
        // Two user prompts carry no duration, so they are diamonds; the other seven draw a bar.
        await Expect(Page.GetByTestId(InspectionTestIds.RunRowDiamond)).ToHaveCountAsync(2);
        await Expect(Page.GetByTestId(InspectionTestIds.RunRowBar)).ToHaveCountAsync(7);
        await Expect(Page.GetByTestId(InspectionTestIds.RunBanner)).ToContainTextAsync("Codex CLI sends events, not spans");
    }
}
