using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem;

// The work item page: header, fields strip and form body.
public sealed class WorkItemScreen(IPage page)
{
    public ILocator Screen => page.GetByTestId(WorkItemTestIds.Screen);

    public ILocator Title => page.GetByTestId(WorkItemTestIds.Title);

    public ILocator Points => page.GetByTestId(WorkItemTestIds.Points);

    public ILocator Tags => page.GetByTestId(WorkItemTestIds.Tags);

    public ILocator TagInput => page.GetByTestId(WorkItemTestIds.TagInput);

    public ILocator Save => page.GetByTestId(WorkItemTestIds.Save);

    public ILocator SaveState => page.GetByTestId(WorkItemTestIds.SaveState);

    public ILocator Back => page.GetByTestId(WorkItemTestIds.Back);

    public ILocator Repro => page.GetByTestId(WorkItemTestIds.Repro);

    public ILocator Severity => page.GetByTestId(WorkItemTestIds.Severity);

    public ILocator Criteria => page.GetByTestId(WorkItemTestIds.Criteria);

    public ILocator Toast => page.GetByTestId(ComponentTestIds.Toast);

    public async Task OpenAsync(string projectKey, string itemKey)
    {
        await page.GotoAsync(Links.WorkItem(projectKey, itemKey));
        await Screen.WaitForAsync();
    }

    public async Task AddTagAsync(string tag)
    {
        await TagInput.FillAsync(tag);
        await TagInput.PressAsync("Enter");
    }

    // Clicks Save and returns the PATCH response for the item.
    public Task<IResponse> SaveAsync(Guid itemId) =>
        page.RunAndWaitForApiAsync($"/api/work-items/{itemId}", "PATCH", () => Save.ClickAsync());
}
