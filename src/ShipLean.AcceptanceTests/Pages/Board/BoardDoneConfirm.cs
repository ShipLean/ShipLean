using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// Moving a card into Done on a Features or Epics board: the question it asks first, and the offer once its children are closed.
public sealed class BoardDoneConfirm(IPage page)
{
    private static bool IsMovePost(IResponse r) =>
        r.Request.Method == "POST" && Regex.IsMatch(new Uri(r.Url).AbsolutePath, "^/api/boards/[^/]+/moves$");

    public ILocator Dialog => page.GetByTestId(BoardTestIds.DoneConfirm);

    public ILocator OpenChildren => Dialog.GetByTestId(BoardTestIds.DoneConfirmChild);

    public ILocator Cancel => page.GetByTestId(BoardTestIds.DoneConfirmCancel);

    public ILocator MoveAnyway => page.GetByTestId(BoardTestIds.DoneConfirmMove);

    // The last cell of the lane: the board's Done column (a Features board has one default lane and no split column).
    public ILocator DoneCell => page.GetByTestId(BoardTestIds.Cell).Last;

    // The card's offer to move it to Done, with its button.
    public static ILocator Offer(ILocator card) => card.GetByTestId(BoardTestIds.CardOffer);

    public static ILocator MoveToDone(ILocator card) => card.GetByTestId(BoardTestIds.CardMoveToDone);

    // Counts the board moves sent from now on.
    public Func<int> CountMoves()
    {
        var count = 0;
        page.Request += (_, request) =>
        {
            if (request.Method == "POST" && Regex.IsMatch(new Uri(request.Url).AbsolutePath, "^/api/boards/[^/]+/moves$")) count++;
        };
        return () => count;
    }

    // Picks the card up with the pointer and puts it down in the middle of Done. No move is sent until the question is answered.
    public async Task DragToDoneAsync(ILocator card)
    {
        await DoneCell.ScrollIntoViewIfNeededAsync();
        var from = (await card.BoundingBoxAsync())!;
        var to = (await DoneCell.BoundingBoxAsync())!;
        var startX = from.X + (from.Width / 2);
        var startY = from.Y + (from.Height / 2);
        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(startX + 6, startY + 6, new() { Steps = 4 });
        await page.Mouse.MoveAsync(to.X + (to.Width / 2), to.Y + (to.Height / 2), new() { Steps = 12 });
        await page.Mouse.UpAsync();
    }

    // Presses the button and returns the moves response.
    // dnd-kit swallows any click for 50 ms after a drop, so a click straight after the dialog opens can be lost: click
    // again until the button is gone (a lost click leaves it standing; a real one closes the dialog and sends the move).
    public Task<IResponse> ClickAndWaitForMoveAsync(ILocator button) =>
        page.RunAndWaitForResponseAsync(
            async () =>
            {
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    await button.ClickAsync();
                    if (await GoneAsync(button))
                    {
                        return;
                    }
                }
            },
            IsMovePost);

    private static async Task<bool> GoneAsync(ILocator button)
    {
        try
        {
            await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 1000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
