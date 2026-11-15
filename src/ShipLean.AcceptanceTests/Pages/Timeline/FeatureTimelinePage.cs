using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Timeline;

// A Feature's full-screen timeline (/p/<project>/items/<key>/timeline) and the Timeline section on the Feature page that opens it.
// Chart marks are told apart by data-key, never by position.
public sealed class FeatureTimelinePage(IPage page)
{
    public ILocator Page => page.GetByTestId(PagesTestIds.FeatureTimelinePage);

    // The Feature page's Timeline section and its Open timeline link.
    public ILocator Section => page.GetByTestId(FeatureTestIds.FeatureTimelineSection);

    public ILocator OpenLink => page.GetByTestId(FeatureTestIds.FeatureTimelineOpen);

    // Key dates appear on the section and on the page: always scoped to one of them.
    public static ILocator KeyDatePoints(ILocator scope) => scope.GetByTestId(TimelineTestIds.KeyDatePoint);

    public ILocator Toolbar => Page.GetByTestId(TimelineTestIds.Toolbar);

    public ILocator Chart => Page.GetByTestId(TimelineTestIds.Chart);

    public ILocator Bars => Page.GetByTestId(TimelineTestIds.ChartBar);

    public ILocator Bar(string key) => Page.Locator($"[data-testid='{TimelineTestIds.ChartBar}'][data-key='{key}']");

    public ILocator Rail => Page.GetByTestId(TimelineTestIds.Rail);

    public ILocator RailSelected => Page.GetByTestId(TimelineTestIds.RailSelected);

    public ILocator HistoryRows => RailSelected.GetByTestId(TimelineTestIds.RailHistoryRow);

    public ILocator ReviewRows => RailSelected.GetByTestId(TimelineTestIds.RailReviewRow);

    public ILocator Json => Page.GetByTestId(TimelineTestIds.Json);

    // The toolbar's Group and View buttons carry data-value; the toolbar root mirrors the chosen option in data-group and data-view.
    public ILocator GroupButton(string value) => Page.GetByTestId(TimelineTestIds.ToolbarGroup).Locator($"button[data-value='{value}']");

    public ILocator ViewButton(string value) => Page.GetByTestId(TimelineTestIds.ToolbarView).Locator($"button[data-value='{value}']");

    public async Task OpenFromFeatureAsync()
    {
        await OpenLink.ClickAsync();
        await Bars.First.WaitForAsync();
    }
}
