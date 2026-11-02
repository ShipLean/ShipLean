using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Filling;

// What the filling journeys do on the page and read back from the server.
internal static class FillingSteps
{
    public static string ItemsPath => $"/api/projects/{SeedIds.Projects.Ship}/backlog/items";

    // Puts keyboard focus on a row, as a click on its blank space does.
    public static async Task FocusRowAsync(StoriesBacklog backlog, string itemKey)
    {
        await backlog.Row(itemKey).FocusAsync();
        await Assertions.Expect(backlog.Row(itemKey)).ToBeFocusedAsync();
    }

    // Types a title in the open add row and presses Enter; returns the create response.
    public static Task<IResponse> TypeAsync(IPage page, StoriesBacklog backlog, string title) =>
        page.RunAndWaitForApiAsync(ItemsPath, "POST", async () =>
        {
            await backlog.AddTitle.FillAsync(title);
            await backlog.AddTitle.PressAsync("Enter");
        });

    // The keys of the rows a server answer created, in its order.
    public static async Task<List<string>> CreatedKeysAsync(IResponse response)
    {
        var body = System.Text.Json.JsonDocument.Parse(await response.TextAsync());
        return body.RootElement.GetProperty("created").EnumerateArray().Select(c => c.GetProperty("key").GetString()!).ToList();
    }

    // The titles of the rows, in order.
    public static List<string> Titles(IEnumerable<BacklogEntry> entries) => entries.Select(e => e.Title).ToList();

    // Whether the dialog or the page scrolls sideways at this width.
    public static async Task<bool> OverflowsAtAsync(IPage page, StoriesBacklog backlog, ILocator element, int width)
    {
        await page.SetViewportSizeAsync(width, 900);
        return await backlog.OverflowsAsync() || await backlog.OverflowsAsync(element);
    }
}
