using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// Moving rows on the Planning view: the grips, the rows by section, the drop targets and the toasts.
public sealed class PlanningDragging(IPage page)
{
    public ILocator Rows => page.GetByTestId(BacklogTestIds.Row);

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.ToastHost).GetByTestId(ComponentTestIds.Toast);

    // The row of an item, by its key.
    public ILocator Row(string itemKey) =>
        Rows.Filter(new() { Has = page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new Regex($"^{itemKey}$") }) });

    // The header of a section by its title ("Sprint 25", "Backlog"), which is also where a row is dropped to change only its sprint.
    public ILocator Header(string title) =>
        page.GetByTestId(BacklogTestIds.PlanningSection)
            .Filter(new() { Has = page.GetByTestId(BacklogTestIds.PlanningSectionTitle).Filter(new() { HasTextRegex = new Regex($"^{title}$") }) });

    // The keys of a section's rows, top to bottom, read from the rows' section attribute.
    public async Task<List<string>> KeysInAsync(string sectionId)
    {
        var pairs = await Rows.EvaluateAllAsync<string[][]>(
            "rows => rows.map(r => [r.getAttribute('data-key'), r.getAttribute('data-section')])");
        return pairs.Where(p => p[1] == sectionId).Select(p => p[0]).ToList();
    }

    // Waits until the page shows `itemKey` straight above `belowKey` (the reads that follow a teammate's two writes arrive one at a time).
    public Task WaitForRowAboveAsync(string itemKey, string belowKey) =>
        page.WaitForFunctionAsync(
            "([testId, above, below]) => { const keys = [...document.querySelectorAll(`[data-testid=\"${testId}\"]`)].map(r => r.getAttribute('data-key')); return keys.indexOf(above) >= 0 && keys.indexOf(above) + 1 === keys.indexOf(below); }",
            new object[] { BacklogTestIds.Row, itemKey, belowKey });

    public Task<string?> SectionOfAsync(string itemKey) => Row(itemKey).GetAttributeAsync("data-section");

    // Makes the page tall enough to show every section, so a drag never scrolls it and the rows stay where they were measured.
    public Task ShowWholePageAsync() => page.SetViewportSizeAsync(1280, 2400);

    // Picks the row up by its grip and puts it down on `target` (its lower half when `below`), once the page says the target is the one under the pointer.
    public async Task DragAsync(string itemKey, ILocator target, bool below = false)
    {
        var grip = (await Row(itemKey).GetByTestId(BacklogTestIds.Grip).BoundingBoxAsync())!;
        var box = (await target.BoundingBoxAsync())!;
        var startX = grip.X + (grip.Width / 2);
        var startY = grip.Y + (grip.Height / 2);
        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(startX + 5, startY + 10, new() { Steps = 4 });
        await page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2) + (below ? 8 : -8), new() { Steps = 12 });
        await Assertions.Expect(target).ToHaveAttributeAsync("data-over", "true");
        await page.Mouse.UpAsync();
    }

    // Drags the row onto another row and returns the reorder response, which comes after the sprint change.
    public Task<IResponse> DragOntoRowAsync(string itemKey, string targetKey, bool below = false) =>
        page.RunAndWaitForResponseAsync(
            () => DragAsync(itemKey, Row(targetKey), below),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/reorder", StringComparison.Ordinal));

    // Drags the row onto a section's header and returns the sprint change's response.
    public Task<IResponse> DragOntoHeaderAsync(string itemKey, string title) =>
        page.RunAndWaitForResponseAsync(
            () => DragAsync(itemKey, Header(title)),
            r => r.Request.Method == "PATCH" && r.Url.Contains("/api/work-items/", StringComparison.Ordinal));
}
