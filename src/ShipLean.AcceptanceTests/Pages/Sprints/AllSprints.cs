using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Sprints;

// The All sprints page: the velocity chart, the sprint list and the New sprint rail.
public sealed class AllSprints(IPage page)
{
    public ILocator List => page.GetByTestId(SprintsTestIds.List);

    public ILocator Rows => page.GetByTestId(SprintsTestIds.ListRow);

    public ILocator RowOf(string sprintName) => Rows.Filter(new() { Has = page.GetByTestId(SprintsTestIds.RowLink).Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(sprintName)}$") }) });

    public ILocator CapacityCells => page.GetByTestId(SprintsTestIds.RowCapacity);

    public ILocator VelocityAverage => page.GetByTestId(SprintsTestIds.VelocityAverage);

    public ILocator VelocityBars => page.GetByTestId(SprintsTestIds.VelocityBar);

    public ILocator Name => page.GetByTestId(SprintsTestIds.NewSprintName);

    public ILocator Start => page.GetByTestId(SprintsTestIds.NewSprintStart);

    public ILocator End => page.GetByTestId(SprintsTestIds.NewSprintEnd);

    public ILocator Error => page.GetByTestId(SprintsTestIds.NewSprintError);

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.Sprints(projectKey));
        await List.WaitForAsync();
        await Name.WaitForAsync();
    }

    // Presses Create sprint and returns the create response.
    public Task<IResponse> CreateAsync(string projectId) =>
        page.RunAndWaitForApiAsync($"/api/projects/{projectId}/sprints", "POST", () => page.GetByTestId(SprintsTestIds.NewSprintSubmit).ClickAsync());
}
