using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The All sprints page at 900px and below (SHIP-723): sprint rows, the velocity card and + New sprint, whose form opens
// full screen with Create at the top right.
public sealed class AllSprintsPhone(IPage page)
{
    public ILocator List => page.GetByTestId(SprintsTestIds.ListTouch);

    public ILocator Rows => page.GetByTestId(SprintsTestIds.ListRow);

    public ILocator Toolbar => page.GetByTestId(SprintsTestIds.NewSprintToolbar);

    public ILocator Velocity => page.GetByTestId(SprintsTestIds.Velocity);

    public ILocator NewSprintButton => page.GetByTestId(SprintsTestIds.NewSprintOpen);

    public ILocator Dialog => page.GetByTestId(ComponentTestIds.Dialog);

    public ILocator Name => page.GetByTestId(SprintsTestIds.NewSprintName);

    public ILocator CreateButton => page.GetByTestId(SprintsTestIds.NewSprintCreate);

    public ILocator RowOf(string sprintName) => Rows.Filter(new() { HasText = sprintName });

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.Sprints(projectKey));
        await List.WaitForAsync();
        await NewSprintButton.WaitForAsync();
    }

    // Taps Create in the dialog's header and returns the create response.
    public Task<IResponse> CreateAsync(string projectId) =>
        page.RunAndWaitForApiAsync($"/api/projects/{projectId}/sprints", "POST", () => CreateButton.TapAsync());
}
