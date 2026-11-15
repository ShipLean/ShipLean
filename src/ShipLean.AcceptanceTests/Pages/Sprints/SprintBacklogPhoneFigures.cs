using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Sprint backlog's figures, burndown and glance rows at 900px and below (SHIP-724).
public sealed class SprintBacklogPhoneFigures(IPage page)
{
    public ILocator Figures => page.GetByTestId(SprintsTestIds.Figures);

    // The four figure values, one per tile.
    public ILocator Tiles => Committed.Or(page.GetByTestId(SprintsTestIds.KpiCompleted)).Or(page.GetByTestId(SprintsTestIds.KpiRemaining)).Or(page.GetByTestId(SprintsTestIds.KpiDaysLeft));

    public ILocator Committed => page.GetByTestId(SprintsTestIds.KpiCommitted);

    public ILocator PointsBar => page.GetByTestId(SprintsTestIds.PointsBar);

    public ILocator Glance => page.GetByTestId(SprintsTestIds.GlanceRows);

    public ILocator ReadyRow => page.GetByTestId(SprintsTestIds.GlanceReady);

    public ILocator ActiveRow => page.GetByTestId(SprintsTestIds.GlanceActive);

    public ILocator Table => page.GetByTestId(SprintsTestIds.Table);

    // The sheet's people, each with the item keys listed under their name.
    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator People => Sheet.GetByTestId(SprintsTestIds.GlancePerson);

    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintBacklog(projectKey, sprintId));
        await Committed.WaitForAsync();
        await Table.WaitForAsync();
    }

    // user id -> item keys under that person's name, from what the sheet shows.
    public async Task<Dictionary<string, string[]>> SheetItemsAsync()
    {
        var people = await People.EvaluateAllAsync<string[][]>(
            "els => els.map(e => [e.dataset.userId, ...[...e.querySelectorAll('[data-item-key]')].map(a => a.dataset.itemKey)])");
        return people.ToDictionary(p => p[0], p => p.Skip(1).ToArray());
    }

    // True when the page scrolls sideways: the document is wider than the screen.
    public Task<bool> ScrollsSidewaysAsync() =>
        page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
}
