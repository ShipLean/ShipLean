using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
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

    public ILocator AddMany => page.GetByTestId(BacklogTestIds.AddMany);

    public ILocator AddHere(string sectionTitle) =>
        page.GetByTestId(BacklogTestIds.PlanningSection)
            .Filter(new() { Has = page.GetByTestId(BacklogTestIds.PlanningSectionTitle).Filter(new() { HasTextRegex = new Regex($"^{sectionTitle}$") }) })
            .GetByTestId(BacklogTestIds.AddHere);

    // A row by its key.
    public ILocator Row(string itemKey) =>
        Rows.Filter(new() { Has = RowKeys.Filter(new() { HasTextRegex = new Regex($"^{itemKey}$") }) });

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

    // The backlog read model as the server answers it now, in a mode ("list" or "planning").
    public async Task<JsonElement> ViewAsync(string mode)
    {
        var response = await page.Context.APIRequest.GetAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/backlog?mode={mode}&filter=&includeCompleted=false");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    // The whole stack top to bottom as the server holds it (list mode).
    public async Task<List<BacklogEntry>> StackAsync() =>
        (await ViewAsync("list")).GetProperty("rows").EnumerateArray().Select(BacklogEntry.From).ToList();

    // One planning section's rows top to bottom, by section title ("Sprint 25", "Backlog").
    public async Task<List<BacklogEntry>> SectionAsync(string title) =>
        (await ViewAsync("planning")).GetProperty("sections").EnumerateArray()
            .Single(section => section.GetProperty("title").GetString() == title)
            .GetProperty("rows").EnumerateArray().Select(BacklogEntry.From).ToList();

    // Scrolls sideways or not: true when the page or the element is wider than what shows.
    public Task<bool> OverflowsAsync(ILocator? within = null) =>
        within is null
            ? page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth")
            : within.EvaluateAsync<bool>("el => el.scrollWidth > el.clientWidth");
}

// A row of the backlog read model, reduced to what the journeys compare.
public sealed record BacklogEntry(string Key, string Title, string Type, string? ParentKey, string? Sprint)
{
    public static BacklogEntry From(JsonElement row) => new(
        row.GetProperty("key").GetString()!,
        row.GetProperty("title").GetString()!,
        row.GetProperty("type").GetProperty("id").GetString()!,
        row.GetProperty("parent") is { ValueKind: JsonValueKind.Object } parent ? parent.GetProperty("key").GetString() : null,
        row.GetProperty("sprint") is { ValueKind: JsonValueKind.Object } sprint ? sprint.GetProperty("name").GetString() : null);
}
