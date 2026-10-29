using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// Moving cards on the sprint Taskboard or a Feature board (same grid, its own moves endpoint): drag, Pick up, Move to Resolved, and the toast a refused move leaves.
public sealed class TaskboardMoves(IPage page)
{
    // The column of each Task state in the seeded Storefront project, left to right.
    public const int NewColumn = 0;
    public const int ActiveColumn = 1;
    public const int InReviewColumn = 2;
    public const int ClosedColumn = 3;

    private static bool IsMovePost(IResponse r) =>
        r.Request.Method == "POST"
        && (new Uri(r.Url).AbsolutePath.EndsWith("/taskboard/moves", StringComparison.Ordinal)
            || new Uri(r.Url).AbsolutePath.EndsWith("/feature-board/moves", StringComparison.Ordinal));

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.ToastHost).GetByTestId(ComponentTestIds.Toast);

    // The card of a task, by its key.
    public ILocator Card(string key) =>
        page.GetByTestId(TaskboardTestIds.Card)
            .Filter(new() { Has = page.GetByTestId(TaskboardTestIds.CardKey).Filter(new() { HasTextRegex = new Regex($"^{key}$") }) });

    // The cell of a column in the row whose header holds this title.
    public static ILocator Cell(ILocator row, int column) => row.GetByTestId(TaskboardTestIds.Cell).Nth(column);

    // Picks the card up with the pointer and puts it down on the bottom edge of the cell; returns the moves response.
    public async Task<IResponse> DragToCellAsync(ILocator card, ILocator cell)
    {
        await cell.ScrollIntoViewIfNeededAsync();
        var from = (await card.BoundingBoxAsync())!;
        var to = (await cell.BoundingBoxAsync())!;
        var startX = from.X + (from.Width / 2);
        var startY = from.Y + (from.Height / 2);
        return await page.RunAndWaitForResponseAsync(
            async () =>
            {
                await page.Mouse.MoveAsync(startX, startY);
                await page.Mouse.DownAsync();
                await page.Mouse.MoveAsync(startX + 6, startY + 6, new() { Steps = 4 });
                await page.Mouse.MoveAsync(to.X + (to.Width / 2), to.Y + to.Height - 8, new() { Steps = 12 });
                await page.Mouse.UpAsync();
            },
            IsMovePost);
    }

    // Presses a key chord on the focused card; returns the moves response.
    public Task<IResponse> KeyMoveAsync(string chord) => page.RunAndWaitForResponseAsync(() => page.Keyboard.PressAsync(chord), IsMovePost);

    // Hovers the card to reveal Pick up and clicks it; returns the moves response.
    public async Task<IResponse> PickUpAsync(ILocator card)
    {
        await card.HoverAsync();
        return await page.RunAndWaitForResponseAsync(() => card.GetByTestId(TaskboardTestIds.CardPickUp).ClickAsync(), IsMovePost);
    }

    // Clicks "Move story to Resolved" in the row; returns the work item PATCH response.
    public Task<IResponse> MoveToResolvedAsync(ILocator row) =>
        page.RunAndWaitForResponseAsync(
            () => row.GetByTestId(TaskboardTestIds.RowOfferResolve).ClickAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));
}
