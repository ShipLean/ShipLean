using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Sprint backlog's list at 900px and below (SHIP-724): two-line rows, the States sheet and the Tasks switch.
public sealed class SprintBacklogPhoneRows(IPage page)
{
    public ILocator List => page.GetByTestId(SprintsTestIds.Table);

    public ILocator StoryRows => page.GetByTestId(SprintsTestIds.StoryRow);

    public ILocator TaskRows => page.GetByTestId(SprintsTestIds.TaskRow);

    public ILocator TasksSwitch => page.GetByTestId(SprintsTestIds.ShowTasks);

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator Toast => page.GetByTestId(ComponentTestIds.Toast);

    public ILocator Row(string itemKey) => StoryRows.Or(TaskRows).Filter(new() { HasText = itemKey });

    public ILocator RowState(string itemKey) => Row(itemKey).GetByTestId(ComponentTestIds.StateDot);

    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintBacklog(projectKey, sprintId));
        await List.WaitForAsync();
        await StoryRows.First.WaitForAsync();
    }

    // Taps the row's state pill, then the state in the States sheet; returns the work item PATCH response.
    public async Task<IResponse> PickStateAsync(string itemKey, string stateName)
    {
        await Row(itemKey).GetByTestId(SprintsTestIds.StateButton).TapAsync();
        return await page.RunAndWaitForResponseAsync(
            () => Sheet.GetByTestId(SprintsTestIds.StatesSheetOption).Filter(new() { HasText = stateName }).TapAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));
    }
}
