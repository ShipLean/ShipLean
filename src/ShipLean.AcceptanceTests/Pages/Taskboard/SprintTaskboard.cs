using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// The sprint Taskboard grid: the toolbar, the rows and their cells.
public sealed class SprintTaskboard(IPage page)
{
    public ILocator Grid => page.GetByTestId(TaskboardTestIds.Grid);

    public ILocator Rows => page.GetByTestId(TaskboardTestIds.Row);

    // The row whose header holds this title (a story's or bug's title, "Unparented", or a person's name).
    public ILocator RowTitled(string title) =>
        Rows.Filter(new() { Has = page.GetByTestId(TaskboardTestIds.RowTitle).Filter(new() { HasTextString = title }) });

    public ILocator Cards => page.GetByTestId(TaskboardTestIds.Card);

    public ILocator GroupOption(string option) =>
        page.GetByTestId(TaskboardTestIds.GroupOption).Filter(new() { HasText = option });

    public ILocator TaskTitle => page.GetByTestId(TaskboardTestIds.TaskTitle);

    public async Task OpenAsync(string projectKey, Guid sprintId)
    {
        await page.GotoAsync(Links.Taskboard(projectKey, sprintId));
        await Grid.WaitForAsync();
    }

    // Switches Group by and waits for the preference to be saved.
    public Task<IResponse> GroupByAsync(string option) =>
        page.RunAndWaitForApiAsync("/api/me/preferences/view%3Ataskboard", "PUT", () => GroupOption(option).ClickAsync());

    // Types a title in the open task box and presses Enter; returns the create response.
    public async Task<IResponse> AddTaskAsync(string title)
    {
        await TaskTitle.FillAsync(title);
        return await page.RunAndWaitForApiAsync("/api/work-items", "POST", () => TaskTitle.PressAsync("Enter"));
    }

    public ILocator TaskCard(string key) =>
        Cards.Filter(new() { Has = page.GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{System.Text.RegularExpressions.Regex.Escape(key)}$") }) });
}
