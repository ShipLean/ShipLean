using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// Real key presses on the Stories list and Planning: getting focus onto the list the way a keyboard user does, then the keys.
public sealed class BacklogKeys(IPage page)
{
    private const int MaxTabs = 40;

    public ILocator SearchBox => page.GetByTestId(BacklogTestIds.SearchBox);

    public ILocator Dialog => page.GetByTestId(BacklogTestIds.KeysDialog);

    public Task PressAsync(string key) => page.Keyboard.PressAsync(key);

    // The key of the row that has focus, or null when focus is not on a row.
    public Task<string?> FocusedRowKeyAsync() =>
        page.EvaluateAsync<string?>(
            "testId => { const e = document.activeElement; return e && e.getAttribute('data-testid') === testId ? e.getAttribute('data-key') : null; }",
            BacklogTestIds.Row);

    // Starts from the search box, which comes before the list in the tab order, and presses Tab until a row has focus.
    // Returns how many presses it took; fails if the list is not reachable by Tab.
    public async Task<int> TabToListAsync()
    {
        await SearchBox.FocusAsync();
        for (var presses = 1; presses <= MaxTabs; presses++)
        {
            await page.Keyboard.PressAsync("Tab");
            if (await FocusedRowKeyAsync() is not null)
            {
                return presses;
            }
        }

        throw new InvalidOperationException($"Tab did not reach a list row in {MaxTabs} presses.");
    }

    // Walks to a row with J or K from the row that has focus.
    public async Task WalkToAsync(string itemKey)
    {
        var rows = await page.GetByTestId(BacklogTestIds.Row).EvaluateAllAsync<string[]>("rows => rows.map(r => r.getAttribute('data-key'))");
        var from = Array.IndexOf(rows, await FocusedRowKeyAsync());
        var to = Array.IndexOf(rows, itemKey);
        from.ShouldBeGreaterThanOrEqualTo(0);
        to.ShouldBeGreaterThanOrEqualTo(0);
        for (var step = 0; step < Math.Abs(to - from); step++)
        {
            await page.Keyboard.PressAsync(to > from ? "j" : "k");
        }

        await Assertions.Expect(page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextString = itemKey }) })).ToBeFocusedAsync();
    }
}
