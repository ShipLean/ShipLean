using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// The Stories backlog's touch rows (900px and below, SHIP-716): the row list in place of the table.
public sealed class TouchRows(IPage page)
{
    public ILocator List => page.GetByTestId(BacklogTestIds.TouchRowList);

    public ILocator Rows => List.GetByTestId(BacklogTestIds.Row);

    // The row with this key.
    public ILocator Row(string key) =>
        Rows.Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(key)}$") }) });

    // The drag handle in the left-edge slot of the row with this key.
    public ILocator Handle(string key) => Row(key).GetByTestId(BacklogTestIds.TouchHandle);

    // The keys of the rows, top to bottom.
    public async Task<IReadOnlyList<string>> KeysAsync() =>
        await Rows.GetByTestId(BacklogTestIds.Key).AllInnerTextsAsync();

    // Picks the row `key` up by its handle and drops it on the row `targetKey` with a real touch drag, then returns the
    // reorder response.
    public async Task<IResponse> DragByHandleAsync(string key, string targetKey) =>
        await page.RunAndWaitForResponseAsync(
            () => TouchDrag.ByHandleAsync(page, Handle(key), Row(targetKey)),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/reorder", StringComparison.Ordinal));

    // How many <table> elements the row list holds (none at touch).
    public async Task<int> TableCountAsync() =>
        await List.EvaluateAsync<int>("element => element.querySelectorAll('table').length");

    // How far the row list's content is wider than the list itself (0 when nothing scrolls sideways).
    public async Task<int> SidewaysOverflowAsync() =>
        await List.EvaluateAsync<int>("element => element.scrollWidth - element.clientWidth");
}
