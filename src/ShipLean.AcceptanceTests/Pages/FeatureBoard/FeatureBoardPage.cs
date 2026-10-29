using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.FeatureBoard;

// A Feature's board: the Taskboard grid scoped to one Feature, reached from the Task board link on its form. The grid,
// rows, cards and moves are the Taskboard's, so this wraps the Taskboard page objects and adds the Feature-only parts.
public sealed class FeatureBoardPage(IPage page)
{
    public SprintTaskboard Board { get; } = new(page);

    public TaskboardMoves Moves { get; } = new(page);

    public ILocator Header => page.GetByTestId(FeatureBoardTestIds.Header);

    public ILocator Page => page.GetByTestId(PagesTestIds.FeatureBoardPage);

    // The Task board link in the Stories and bugs header of the Feature form.
    public ILocator TaskBoardLink => page.GetByTestId(FeatureTestIds.TaskBoardLink);

    // The sprint pill in a row header: the sprint's name, or "Backlog".
    public static ILocator SprintPill(ILocator row) => row.GetByTestId(TaskboardTestIds.RowSprint);

    public async Task OpenFeatureAsync(string projectKey, string featureKey)
    {
        await page.GotoAsync(Links.WorkItem(projectKey, featureKey));
        await TaskBoardLink.WaitForAsync();
    }

    // Clicks Task board on the Feature form and waits for the grid.
    public async Task FollowTaskBoardLinkAsync()
    {
        await TaskBoardLink.ClickAsync();
        await Board.Grid.WaitForAsync();
    }

    // The sprint a work item is in now, or null for the Backlog, read from the API.
    public async Task<Guid?> SprintOfAsync(string projectKey, string itemKey)
    {
        var response = await page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{itemKey}");
        if (!response.Ok)
        {
            throw new InvalidOperationException($"{projectKey} {itemKey}: {response.Status} {await response.TextAsync()}");
        }
        var sprint = JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("sprint");
        return sprint.ValueKind == JsonValueKind.Null ? null : sprint.GetProperty("id").GetGuid();
    }
}
