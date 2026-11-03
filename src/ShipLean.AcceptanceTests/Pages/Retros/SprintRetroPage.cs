using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// A sprint's Retro tab: the header and KPIs, the By category / By work item toggle, the To decide filter, the two views and the rail.
public sealed class SprintRetroPage(IPage page)
{
    public ILocator Panel => page.GetByTestId(RetrosTestIds.SprintRetroPanel);

    public ILocator Header => page.GetByTestId(RetrosTestIds.RetroHeader);

    public ILocator KpiWithRetro => page.GetByTestId(RetrosTestIds.RetroKpiWithRetro);

    public ILocator KpiRecurring => page.GetByTestId(RetrosTestIds.RetroKpiRecurring);

    public ILocator KpiToDecide => page.GetByTestId(RetrosTestIds.RetroKpiToDecide);

    public ILocator ToDecideFilter => page.GetByTestId(RetrosTestIds.RetroToDecideFilter);

    public ILocator KpiActions => page.GetByTestId(RetrosTestIds.RetroKpiActions);

    public ILocator ViewByCategory => page.GetByTestId(RetrosTestIds.RetroViewByCategory);

    public ILocator ViewByItem => page.GetByTestId(RetrosTestIds.RetroViewByItem);

    public ILocator WrittenByFilter => page.GetByTestId(RetrosTestIds.RetroWrittenByFilter);

    public ILocator Columns => page.GetByTestId(RetroSharedTestIds.RetroBoardColumn);

    public ILocator ThemeGroups => page.GetByTestId(RetrosTestIds.RetroThemeGroup);

    public ILocator Points => page.GetByTestId(RetroSharedTestIds.RetroNote);

    public ILocator PointOf(string text) => Points.Filter(new() { HasText = text });

    public ILocator MoveTheme(string pointText) => PointOf(pointText).GetByTestId(RetroSharedTestIds.RetroNoteTheme);

    public ILocator AddAction(string pointText) => PointOf(pointText).GetByTestId(RetroSharedTestIds.RetroNoteAddAction);

    public ILocator Archive(string pointText) => PointOf(pointText).GetByTestId(RetroSharedTestIds.RetroNoteArchive);

    public RetroBoardPart Board => new(Panel);

    public RetroActionDialogPart Dialog => new(page);

    public ILocator CardList => page.GetByTestId(RetrosTestIds.RetroCardList);

    public ILocator Cards => page.GetByTestId(RetroSharedTestIds.RetroCard);

    public ILocator Rail => page.GetByTestId(RetrosTestIds.RetroRail);

    public ILocator Coverage => page.GetByTestId(RetrosTestIds.RetroCoverage);

    public ILocator CoverageRows => page.GetByTestId(RetrosTestIds.RetroCoverageRow);

    public ILocator RailActions => page.GetByTestId(RetrosTestIds.RetroRailActions);

    public ILocator RailRecurring => page.GetByTestId(RetrosTestIds.RetroRailRecurring);

    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintRetro(projectKey, sprintId));
        await Panel.WaitForAsync();
    }
}
