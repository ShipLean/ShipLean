using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The Stories backlog: its toolbar, the list rows and the first-run state.
public sealed class StoriesBacklog(IPage page)
{
    private static readonly string[] ColumnLabels = ["#", "Key", "Title", "Parent", "State", "Points", "Sprint", "Assignee", "Tags"];

    public ILocator Toolbar => page.GetByTestId(BacklogTestIds.Toolbar);

    public ILocator List => page.GetByTestId(BacklogTestIds.List);

    public ILocator Rows => page.GetByTestId(BacklogTestIds.Row);

    public ILocator RowKeys => page.GetByTestId(BacklogTestIds.Key);

    public ILocator ColumnHeaders => page.GetByTestId(BacklogTestIds.ColumnHeader);

    // The header of a column, by its label ("Parent", "Sprint").
    public ILocator ColumnHeader(string label) => ColumnHeaders.Filter(new() { HasTextRegex = new Regex($"^{label}$") });

    public ILocator NewItem => page.GetByTestId(BacklogTestIds.NewItem);

    public ILocator AddRow => page.GetByTestId(BacklogTestIds.AddRow);

    public ILocator AddType => page.GetByTestId(BacklogTestIds.AddType);

    public ILocator AddTitle => page.GetByTestId(BacklogTestIds.AddTitle);

    public ILocator ColumnsButton => page.GetByTestId(BacklogTestIds.ColumnsButton);

    // The Columns menu's checkbox for a column, by its label; the menu lists them in the list's column order.
    public ILocator ColumnOption(string label) =>
        page.GetByTestId(BacklogTestIds.ColumnOption).Nth(Array.IndexOf(ColumnLabels, label));

    public ILocator Empty => page.GetByTestId(BacklogTestIds.Empty);

    public ILocator EmptyCreate => page.GetByTestId(BacklogTestIds.EmptyCreate);

    public ILocator EmptyImport => page.GetByTestId(BacklogTestIds.EmptyImport);

    public async Task OpenAsync(string projectKey)
    {
        await page.GotoAsync(Links.Backlog(projectKey));
        await Toolbar.WaitForAsync();
    }

    // Types a title in the open new-item row and presses Enter; returns the create response.
    public async Task<IResponse> AddAsync(string title)
    {
        await AddTitle.FillAsync(title);
        return await page.RunAndWaitForApiAsync("/api/work-items", "POST", () => AddTitle.PressAsync("Enter"));
    }

    // Unticks or ticks a column in the Columns menu and waits for the preference to be saved.
    public async Task<IResponse> ToggleColumnAsync(string label)
    {
        if (!await page.GetByTestId(BacklogTestIds.ColumnsMenu).IsVisibleAsync())
        {
            await ColumnsButton.ClickAsync();
        }

        return await page.RunAndWaitForApiAsync(
            "/api/me/preferences/view%3Abacklog",
            "PUT",
            () => ColumnOption(label).ClickAsync());
    }
}
