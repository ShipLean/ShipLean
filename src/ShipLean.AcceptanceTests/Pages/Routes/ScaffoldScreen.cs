using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Routes;

// Any routed page's scaffold: its root, the not-found state and the named slots.
public sealed class ScaffoldScreen(IPage page)
{
    public ILocator SprintBacklogPage => page.GetByTestId(PagesTestIds.SprintBacklogPage);

    public ILocator SprintBacklogSlot => page.GetByTestId(PagesTestIds.SlotSprintBacklog);

    public ILocator NotFound => page.GetByTestId(PagesTestIds.NotFound);

    public static string SprintBacklogPath(string key, Guid sprintId) =>
        $"/p/{Uri.EscapeDataString(key)}/sprints/{sprintId}/backlog";

    public static string BacklogPath(string key) => $"/p/{Uri.EscapeDataString(key)}/backlogs/stories";

    public async Task OpenAsync(string path) => await page.GotoAsync(path);
}
