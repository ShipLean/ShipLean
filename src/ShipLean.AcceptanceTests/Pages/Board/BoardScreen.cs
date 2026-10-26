using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.Contracts;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// A level's board. Columns are addressed by name, cards by key or title, cells by the card they hold.
public sealed class BoardScreen(IPage page)
{
    public ILocator View => page.GetByTestId(BoardTestIds.View);

    public ILocator Headers => page.GetByTestId(BoardTestIds.ColumnHeader);

    public async Task OpenAsync(string projectKey, string level) =>
        await page.GotoAsync(Links.Board(projectKey, level));

    private static Regex Exactly(string text) => new($"^{Regex.Escape(text)}$");

    public ILocator Header(string columnName) =>
        Headers.Filter(new LocatorFilterOptions { Has = page.GetByTestId(BoardTestIds.ColumnName).Filter(new LocatorFilterOptions { HasTextRegex = Exactly(columnName) }) });

    public ILocator Count(string columnName) => Header(columnName).GetByTestId(BoardTestIds.ColumnCount);

    public ILocator Card(string key) =>
        page.GetByTestId(BoardTestIds.Card).Filter(new LocatorFilterOptions { Has = page.GetByTestId(BoardTestIds.CardKey).Filter(new LocatorFilterOptions { HasTextRegex = Exactly(key) }) });

    public ILocator CardTitled(string title) =>
        page.GetByTestId(BoardTestIds.Card).Filter(new LocatorFilterOptions { HasText = title });

    // The cell that holds a card with this title.
    public ILocator CellOfTitled(string title) =>
        page.GetByTestId(BoardTestIds.Cell).Filter(new LocatorFilterOptions { Has = CardTitled(title) });

    // The lane (by its header name) that holds a card with this title.
    public ILocator LaneOfTitled(string title) =>
        page.GetByTestId(BoardTestIds.Lane).Filter(new LocatorFilterOptions { Has = CardTitled(title) });

    public static ILocator LaneName(ILocator lane) => lane.GetByTestId(BoardTestIds.LaneName);

    // The WIP limit input of the Columns pane in the settings dialog over the board, by position among the columns that have one.
    public ILocator SettingsWipInput(int index) => page.GetByTestId(BoardSettingsTestIds.ColumnWip).Nth(index);

    public ILocator NewCardOpen => page.GetByTestId(BoardTestIds.NewCardOpen);

    public ILocator NewCardTitle => page.GetByTestId(BoardTestIds.NewCardTitle);

    // Types a title in the first column's "+ New" and waits for the create to be answered.
    public async Task AddCardAsync(string title)
    {
        await NewCardOpen.ClickAsync();
        await NewCardTitle.FillAsync(title);
        var created = await page.RunAndWaitForApiAsync("/api/work-items", "POST", () => NewCardTitle.PressAsync("Enter"));
        created.Ok.ShouldBeTrue();
    }

    // Saves the board's settings through the API from another client, the way a teammate's tab would. `change` edits
    // the settings document; the write is based on the document as it was read.
    public static async Task ChangeSettingsAsync(IBrowserContext context, Guid boardId, Action<JsonNode> change)
    {
        var headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" };
        var read = await context.APIRequest.GetAsync($"/api/boards/{boardId}/settings", new APIRequestContextOptions { Headers = headers });
        read.Ok.ShouldBeTrue();
        var document = JsonNode.Parse(await read.TextAsync());
        var settings = document!["settings"]!;
        var edited = settings.DeepClone();
        change(edited);
        var write = await context.APIRequest.PutAsync($"/api/boards/{boardId}/settings", new APIRequestContextOptions
        {
            DataObject = new { basedOn = settings, settings = edited },
            Headers = headers,
        });
        write.Ok.ShouldBeTrue(await write.TextAsync());
    }

    // Sets a column's WIP limit in a settings document (by the column's name).
    public static void SetWipLimit(JsonNode settings, string columnName, int limit)
    {
        var column = settings["columns"]!.AsArray().Single(c => c!["name"]!.GetValue<string>() == columnName)!;
        column["wipLimit"] = limit;
    }
}
