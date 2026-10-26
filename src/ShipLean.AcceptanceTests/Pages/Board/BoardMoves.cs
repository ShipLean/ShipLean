using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// Moving cards on a level's board: drag with the pointer, and the toasts a move leaves.
public sealed class BoardMoves(IPage page)
{
    // The tracks of the seeded Storefront stories board, left to right (Ready has a Doing and a Done half).
    public const int NewColumn = 0;
    public const int ReadyColumn = 1;
    public const int ReadyDoneHalf = 2;
    public const int InProgressColumn = 3;
    public const int InReviewColumn = 4;
    public const int DoneColumn = 5;

    private static bool IsMovePost(IResponse r) =>
        r.Request.Method == "POST" && Regex.IsMatch(new Uri(r.Url).AbsolutePath, "^/api/boards/[^/]+/moves$");

    public ILocator Toasts => page.GetByTestId(ComponentTestIds.ToastHost).GetByTestId(ComponentTestIds.Toast);

    // A lane, by the name in its header.
    public ILocator Lane(string name) =>
        page.GetByTestId(BoardTestIds.Lane)
            .Filter(new LocatorFilterOptions { Has = page.GetByTestId(BoardTestIds.LaneName).Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") }) });

    // The cell of a track in a lane.
    public ILocator Cell(string laneName, int track) => Lane(laneName).GetByTestId(BoardTestIds.Cell).Nth(track);

    // The assignee avatar of a card.
    public static ILocator Assignee(ILocator card) => card.GetByTestId(BoardTestIds.CardAssignee).GetByTestId(ComponentTestIds.Avatar);

    // Picks the card up with the pointer and puts it down in the middle of the cell; returns the moves response.
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
                await page.Mouse.MoveAsync(to.X + (to.Width / 2), to.Y + (to.Height / 2), new() { Steps = 12 });
                await page.Mouse.UpAsync();
            },
            IsMovePost);
    }
}
