using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Sprint end prompt over All sprints (and the Backlog): the dialog, its rows, the banner Decide later leaves.
public sealed class SprintEndPrompt(IPage page)
{
    public ILocator Dialog => page.GetByTestId(ComponentTestIds.Dialog);

    public ILocator Rows => page.GetByTestId(SprintsTestIds.EndRow);

    public ILocator Row(string itemKey) => Rows.Filter(new() { HasText = itemKey });

    public ILocator DestinationOf(string itemKey) => Row(itemKey).GetByTestId(SprintsTestIds.EndDestination);

    public ILocator After => page.GetByTestId(SprintsTestIds.EndAfter);

    public ILocator Banner => page.GetByTestId(SprintsTestIds.EndBanner);

    public ILocator Review => page.GetByTestId(SprintsTestIds.EndReview);

    public ILocator Done => page.GetByTestId(SprintsTestIds.EndDone);

    public ILocator DoneClose => page.GetByTestId(SprintsTestIds.EndDoneClose);

    public ILocator Apply => page.GetByTestId(SprintsTestIds.EndApply);

    public ILocator Later => page.GetByTestId(SprintsTestIds.EndLater);

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.Sprints(projectKey));
        await page.GetByTestId(SprintsTestIds.List).WaitForAsync();
    }

    // Presses Apply and returns the rollover POST response.
    public Task<IResponse> ApplyAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}/rollover", "POST", () => Apply.ClickAsync());
}
