using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog.Touch;

// The Stories backlog's touch rows (900px and below, SHIP-716): the row list in place of the table.
public sealed class TouchRows(IPage page)
{
    public ILocator List => page.GetByTestId(BacklogTestIds.TouchRowList);

    public ILocator Rows => List.GetByTestId(BacklogTestIds.Row);

    // How many <table> elements the row list holds (none at touch).
    public async Task<int> TableCountAsync() =>
        await List.EvaluateAsync<int>("element => element.querySelectorAll('table').length");

    // How far the row list's content is wider than the list itself (0 when nothing scrolls sideways).
    public async Task<int> SidewaysOverflowAsync() =>
        await List.EvaluateAsync<int>("element => element.scrollWidth - element.clientWidth");
}
