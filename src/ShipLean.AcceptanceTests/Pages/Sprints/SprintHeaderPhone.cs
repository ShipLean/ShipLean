using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The sprint header at 900px and below (SHIP-724): picker sheet, ⋯, the goal and the tabs, on any sprint tab.
public sealed class SprintHeaderPhone(IPage page)
{
    public ILocator Header => page.GetByTestId(SprintHeaderTestIds.Header);

    public ILocator Picker => page.GetByTestId(SprintHeaderTestIds.Picker);

    public ILocator PickerRows => page.GetByTestId(SprintHeaderTestIds.PickerSheetRow);

    public ILocator Goal => page.GetByTestId(SprintHeaderTestIds.Goal);

    public ILocator GoalInput => page.GetByTestId(SprintsTestIds.GoalInput);

    public ILocator Tabs => page.GetByTestId(SprintHeaderTestIds.Tabs);

    public ILocator More => Header.GetByTestId(TouchTestIds.MoreButton);

    // Opens a sprint tab by its address and waits for the header.
    public async Task OpenAsync(string path)
    {
        await page.GotoAsync(path);
        await Header.WaitForAsync();
    }

    // Opens the picker sheet and picks the sprint by name.
    public async Task PickAsync(string sprintName)
    {
        await Picker.TapAsync();
        await PickerRows.Filter(new() { HasText = sprintName }).TapAsync();
    }

    // Opens ⋯ and taps the action with this label.
    public async Task MoreActionAsync(string label)
    {
        await More.TapAsync();
        await new Touch.TouchShell(page).ActionRow(label).TapAsync();
    }

    // Saves the goal from the SaveBar and returns the sprint PATCH response.
    public Task<IResponse> SaveGoalAsync(Guid sprintId) =>
        page.RunAndWaitForApiAsync($"/api/sprints/{sprintId}", "PATCH", () => new Touch.TouchShell(page).SaveBarSave.TapAsync());
}
