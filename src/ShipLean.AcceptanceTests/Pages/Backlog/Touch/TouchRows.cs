using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// The Stories backlog's touch rows (900px and below, SHIP-716): the row list in place of the table.
public sealed class TouchRows(IPage page)
{
    public ILocator List => page.GetByTestId(BacklogTestIds.TouchRowList);

    public ILocator Rows => List.GetByTestId(BacklogTestIds.Row);

    // The drag handle of each row's left-edge slot.
    public ILocator Handles => Rows.GetByTestId(BacklogTestIds.TouchHandle);

    // The keys of the rows, top to bottom.
    public async Task<IReadOnlyList<string>> KeysAsync() =>
        await Rows.GetByTestId(BacklogTestIds.Key).AllInnerTextsAsync();

    // Picks the row at `from` up by its handle and drops it on the row at `to` (zero-based) with a real touch drag, then
    // returns the reorder response.
    public async Task<IResponse> DragByHandleAsync(int from, int to) =>
        await page.RunAndWaitForResponseAsync(
            () => TouchDrag.ByHandleAsync(page, Handles.Nth(from), Rows.Nth(to)),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/reorder", StringComparison.Ordinal));

    // How many <table> elements the row list holds (none at touch).
    public async Task<int> TableCountAsync() =>
        await List.EvaluateAsync<int>("element => element.querySelectorAll('table').length");

    // How far the row list's content is wider than the list itself (0 when nothing scrolls sideways).
    public async Task<int> SidewaysOverflowAsync() =>
        await List.EvaluateAsync<int>("element => element.scrollWidth - element.clientWidth");
}
