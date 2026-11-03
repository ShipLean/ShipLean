using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The Close sprint button in the sprint header, and the dialog it opens.
public sealed class SprintEndPrompt(IPage page)
{
    public ILocator Dialog => page.GetByTestId(ComponentTestIds.Dialog);

    public ILocator Rows => page.GetByTestId(SprintsTestIds.EndRow);

    public ILocator Row(string itemKey) => Rows.Filter(new() { HasText = itemKey });

    public ILocator DestinationOf(string itemKey) => Row(itemKey).GetByTestId(SprintsTestIds.EndDestination);

    public ILocator After => page.GetByTestId(SprintsTestIds.EndAfter);

    public ILocator CloseButton => page.GetByTestId(SprintsTestIds.CloseSprint);

    public ILocator Confirm => page.GetByTestId(SprintsTestIds.EndConfirm);

    public ILocator EndsEarly => page.GetByTestId(SprintsTestIds.EndEarly);

    public ILocator Done => page.GetByTestId(SprintsTestIds.EndDone);

    public ILocator DoneClose => page.GetByTestId(SprintsTestIds.EndDoneClose);

    public ILocator Apply => page.GetByTestId(SprintsTestIds.EndApply);

    public ILocator Cancel => page.GetByTestId(SprintsTestIds.EndCancel);

    // The sprint's page, where the button is. The dialog stays closed until PressCloseAsync.
    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.SprintBacklog(projectKey, sprintId));
        await CloseButton.WaitForAsync();
    }

    public async Task PressCloseAsync()
    {
        await CloseButton.ClickAsync();
        await Dialog.WaitForAsync();
    }

    // Presses Apply and returns the rollover POST response.
    public Task<IResponse> ApplyAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}/rollover", "POST", () => Apply.ClickAsync());
}
