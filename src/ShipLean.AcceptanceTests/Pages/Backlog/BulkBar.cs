using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// Selecting rows and the action bar over them: move, remove, the changed-rows report and Undo.
public sealed class BulkBar(IPage page)
{
    public ILocator Bar => page.GetByTestId(BacklogTestIds.BulkBar);

    public ILocator Count => page.GetByTestId(BacklogTestIds.BulkCount);

    public ILocator MoveTop => page.GetByTestId(BacklogTestIds.BulkMoveTop);

    public ILocator MoveBottom => page.GetByTestId(BacklogTestIds.BulkMoveBottom);

    public ILocator Remove => page.GetByTestId(BacklogTestIds.BulkRemove);

    public ILocator Changed => page.GetByTestId(BacklogTestIds.BulkChanged);

    public ILocator ChangedRows => page.GetByTestId(BacklogTestIds.BulkChangedRow);

    public ILocator IncludeAnyway => page.GetByTestId(BacklogTestIds.BulkIncludeAnyway);

    public ILocator Leave => page.GetByTestId(BacklogTestIds.BulkLeave);

    public ILocator Undo => page.GetByTestId(BacklogTestIds.Undo);

    public ILocator SelectAll => page.GetByTestId(BacklogTestIds.SelectAll);

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.ToastHost).GetByTestId(ComponentTestIds.Toast);

    // The row of an item, by its key.
    public ILocator Row(string itemKey) =>
        page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new Regex($"^{itemKey}$") }) });

    // The bar's button that moves the selection to a sprint, by the sprint's name ("Sprint 25").
    public ILocator SprintButton(string name) =>
        page.GetByTestId(BacklogTestIds.BulkMoveSprint).Filter(new() { HasTextRegex = new Regex($"^\\s*{name}") });

    // Ticks each row's box, one click per row, and waits for the bar to count them.
    public async Task SelectAsync(params string[] itemKeys)
    {
        foreach (var key in itemKeys)
        {
            await Row(key).GetByTestId(BacklogTestIds.RowSelect).ClickAsync();
        }

        await Assertions.Expect(Count).ToHaveTextAsync($"{itemKeys.Length} selected");
    }

    // Runs the action and returns the bulk write's response.
    public Task<IResponse> RunBulkAsync(Func<Task> action) =>
        page.RunAndWaitForResponseAsync(
            action,
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

    public Task<IResponse> MoveToSprintAsync(string name) => RunBulkAsync(() => SprintButton(name).ClickAsync());

    public Task<IResponse> UndoAsync() => RunBulkAsync(() => Undo.ClickAsync());

    // The keys of every row the page shows, top to bottom, in the section they sit in.
    public async Task<List<(string Key, string Section)>> RowsAsync()
    {
        var pairs = await page.GetByTestId(BacklogTestIds.Row).EvaluateAllAsync<string[][]>(
            "rows => rows.map(r => [r.getAttribute('data-key'), r.getAttribute('data-section')])");
        return pairs.Select(p => (p[0], p[1])).ToList();
    }

    // Waits until a section shows exactly these keys, top to bottom (the read after a write arrives on its own).
    public Task WaitForSectionAsync(string sectionId, IEnumerable<string> itemKeys) =>
        page.WaitForFunctionAsync(
            "([testId, section, keys]) => { const shown = [...document.querySelectorAll(`[data-testid=\"${testId}\"]`)].filter(r => r.getAttribute('data-section') === section).map(r => r.getAttribute('data-key')); return shown.length === keys.length && shown.every((k, i) => k === keys[i]); }",
            new object[] { BacklogTestIds.Row, sectionId, itemKeys.ToArray() });

    // Ticks the first row, then Shift-clicks the last: the whole range between them is selected.
    public async Task SelectRangeAsync(string firstKey, string lastKey, int count)
    {
        await Row(firstKey).GetByTestId(BacklogTestIds.RowSelect).ClickAsync();
        await Row(lastKey).GetByTestId(BacklogTestIds.RowSelect).ClickAsync(new() { Modifiers = [KeyboardModifier.Shift] });
        await Assertions.Expect(Count).ToHaveTextAsync($"{count} selected");
    }

    // Waits until a section holds this many rows.
    public Task WaitForSectionCountAsync(string sectionId, int count) =>
        page.WaitForFunctionAsync(
            "([testId, section, n]) => [...document.querySelectorAll(`[data-testid=\"${testId}\"]`)].filter(r => r.getAttribute('data-section') === section).length === n",
            new object[] { BacklogTestIds.Row, sectionId, count });
}
