using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Sprint backlog page: the sprint header, the figures and burndown, the table, and the goal editor.
public sealed class SprintBacklogPage(IPage page)
{
    public ILocator Committed => page.GetByTestId(SprintsTestIds.KpiCommitted);

    public ILocator Completed => page.GetByTestId(SprintsTestIds.KpiCompleted);

    public ILocator Remaining => page.GetByTestId(SprintsTestIds.KpiRemaining);

    public ILocator DaysLeft => page.GetByTestId(SprintsTestIds.KpiDaysLeft);

    public ILocator BurndownRemaining => page.GetByTestId(SprintsTestIds.BurndownRemaining);

    public ILocator BurndownPoints => page.GetByTestId(SprintsTestIds.BurndownPoint);

    public ILocator StoryRows => page.GetByTestId(SprintsTestIds.StoryRow);

    public ILocator TaskRows => page.GetByTestId(SprintsTestIds.TaskRow);

    public ILocator StoryRow(string itemKey) => StoryRows.Filter(new() { HasText = itemKey });

    public ILocator Row(string itemKey) => StoryRows.Or(TaskRows).Filter(new() { HasText = itemKey });

    public ILocator UnparentedHeading => page.GetByTestId(SprintsTestIds.UnparentedHeading);

    // The test id of every story row, task row and the unparented heading, top to bottom.
    public Task<string[]> RowKindsAsync() =>
        StoryRows.Or(TaskRows).Or(UnparentedHeading).EvaluateAllAsync<string[]>("rows => rows.map(r => r.dataset.testid)");

    public ILocator StateNotice => page.GetByTestId(SprintsTestIds.StateNotice);

    public ILocator RowState(string itemKey) => Row(itemKey).GetByTestId(ComponentTestIds.StateDot);

    public ILocator Rail => page.GetByTestId(SprintsTestIds.Rail);

    public ILocator Goal => page.GetByTestId(SprintHeaderTestIds.Goal);

    public ILocator GoalEdit => page.GetByTestId(SprintHeaderTestIds.GoalEdit);

    public ILocator GoalInput => page.GetByTestId(SprintsTestIds.GoalInput);

    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintBacklog(projectKey, sprintId));
        await page.GetByTestId(SprintsTestIds.Table).WaitForAsync();
        await Committed.WaitForAsync();
    }

    // Opens the row's state menu, picks the state and returns the work item PATCH response.
    public async Task<IResponse> PickStateAsync(string itemKey, string stateName)
    {
        await Row(itemKey).GetByTestId(SprintsTestIds.StateButton).ClickAsync();
        return await page.RunAndWaitForResponseAsync(
            () => page.GetByTestId(ComponentTestIds.StateMenuOption).Filter(new() { HasText = stateName }).ClickAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));
    }

    // Presses Save and returns the sprint PATCH response.
    public Task<IResponse> SaveGoalAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}", "PATCH", () => page.GetByTestId(SprintsTestIds.GoalSave).ClickAsync());
}
