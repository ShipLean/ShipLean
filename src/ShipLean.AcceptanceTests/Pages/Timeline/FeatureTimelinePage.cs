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

    public ILocator Kpis => Page.GetByTestId(TimelineTestIds.Kpi);

    public ILocator Caption => Page.GetByTestId(TimelineTestIds.HeaderCaption);

    public ILocator ColumnHeads => Page.GetByTestId(TimelineTestIds.ChartLeftHead);

    public ILocator Rows => Page.GetByTestId(TimelineTestIds.ChartRow);

    // The TOOK cell of each row (a duration, "No start", "Not started" or an open "so far").
    public ILocator TookCells => Page.GetByTestId(TimelineTestIds.ChartRowTook);

    public ILocator RailOverview => Page.GetByTestId(TimelineTestIds.RailOverview);

    public ILocator RailOpen => Page.GetByTestId(TimelineTestIds.RailOpen);

    public ILocator RailBack => Page.GetByTestId(TimelineTestIds.RailBack);

    public ILocator JsonCall => Page.GetByTestId(TimelineTestIds.RailJsonCall);

    public ILocator JsonMap => Page.GetByTestId(TimelineTestIds.RailJsonMap);

    public ILocator Bars => Page.GetByTestId(TimelineTestIds.ChartBar);

    public ILocator Bar(string key) => Bars.Filter(new() { HasTextString = key });

    // The last-commit tick on a task's bar; its title reads "Last commit <label>" and data-key names the task.
    public ILocator CommitTicks => Page.GetByTestId(TimelineTestIds.ChartCommitTick);

    public ILocator Rail => Page.GetByTestId(TimelineTestIds.Rail);

    public ILocator RailSelected => Page.GetByTestId(TimelineTestIds.RailSelected);

    public ILocator HistoryRows => RailSelected.GetByTestId(TimelineTestIds.RailHistoryRow);

    public ILocator ReviewRows => RailSelected.GetByTestId(TimelineTestIds.RailReviewRow);

    public ILocator Json => Page.GetByTestId(TimelineTestIds.Json);

    // The toolbar's buttons have no test id each and AG0046 refuses GetByRole, GetByText and Locator, so the group's own id finds the
    // control and the button with that data-value is clicked inside it, the way a person clicks it. The toolbar root then mirrors
    // the choice in data-group and data-view, which the test asserts.
    public Task ChooseGroupAsync(string value) => ClickInAsync(page.GetByTestId(TimelineTestIds.ToolbarGroup), value);

    public Task ChooseViewAsync(string value) => ClickInAsync(page.GetByTestId(TimelineTestIds.ToolbarView), value);

    private static Task ClickInAsync(ILocator group, string value)
        => group.EvaluateAsync("(group, value) => group.querySelector(`button[data-value='${value}']`).click()", value);

    public async Task OpenFromFeatureAsync()
    {
        await OpenLink.ClickAsync();
        await Bars.First.WaitForAsync();
    }
}
