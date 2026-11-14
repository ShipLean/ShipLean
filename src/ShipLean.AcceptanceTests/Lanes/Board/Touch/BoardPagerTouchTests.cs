using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Board.Touch;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.Touch;

// The seeded Storefront Stories board on a phone (SHIP-719 D1, D7): one column a page, chips above with count/limit.
// The touch rules are checked on this task's regions only (the chip row and the pager track). Reads only.
[TestFixture]
public class BoardPagerTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private async Task<(BoardScreen Board, BoardPager Pager)> OpenAsync()
    {
        var board = new BoardScreen(Page);
        var pager = new BoardPager(Page);
        await board.OpenAsync("SHIP", "stories");
        await pager.WaitAsync();
        return (board, pager);
    }

    // The chip row and the track scroll sideways by design (SHIP-805 rule 7), which the touch rules refuse on a region's
    // own box, so the regions are checked through their parts: every chip and the lane headers on the page on screen.
    private static async Task AssertTouchRulesAsync(BoardPager pager)
    {
        var chips = await pager.Chips.CountAsync();
        for (var i = 0; i < chips; i++)
        {
            (await TouchRules.ViolationsAsync(pager.Chips.Page, pager.Chips.Nth(i))).ShouldBeEmpty();
        }
        var headers = pager.Track.GetByTestId(BoardTestIds.ColumnLaneHeader);
        var count = await headers.CountAsync();
        for (var i = 0; i < count; i++)
        {
            (await TouchRules.ViolationsAsync(pager.Chips.Page, headers.Nth(i))).ShouldBeEmpty();
        }
    }

    [Test]
    public async Task Board_WhenChipTappedOnPhone_ShouldTurnToThatColumn()
    {
        var (_, pager) = await OpenAsync();
        var chips = await pager.Chips.CountAsync();
        chips.ShouldBe(5);
        await Expect(pager.Chips.First).ToHaveAttributeAsync("aria-current", "true");
        await Expect(pager.Chip("In review")).ToContainTextAsync(new Regex(@"\d+(/\d+)?$"));
        await AssertTouchRulesAsync(pager);

        await pager.Chip("In review").TapAsync();

        await Expect(pager.Chip("In review")).ToHaveAttributeAsync("aria-current", "true");
        await Expect(pager.Chips.First).Not.ToHaveAttributeAsync("aria-current", "true");
        var target = pager.Pages.Filter(new LocatorFilterOptions
        {
            Has = Page.GetByTestId(BoardTestIds.ColumnName).Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^In review$") }),
        });
        await Expect(target.GetByTestId(BoardTestIds.ColumnHeader)).ToBeInViewportAsync(new LocatorAssertionsToBeInViewportOptions { Ratio = 1 });
    }

    [Test]
    public async Task Board_WhenItemModalClosedOnPhone_ShouldStayOnSameColumn()
    {
        var (_, pager) = await OpenAsync();
        var peek = new ItemPeekDialog(Page);
        var third = pager.Chips.Nth(2);
        await third.TapAsync();
        await Expect(third).ToHaveAttributeAsync("aria-current", "true");
        var column = pager.PageAt(2);
        await AssertTouchRulesAsync(pager);

        await column.GetByTestId(BoardTestIds.CardTitle).First.ClickAsync();
        var close = peek.Dialog.GetByTestId(TouchTestIds.DialogClose);
        await close.WaitForAsync();
        await close.TapAsync();
        await peek.WaitClosedAsync();

        await Expect(third).ToHaveAttributeAsync("aria-current", "true");
        await Expect(column.GetByTestId(BoardTestIds.ColumnHeader)).ToBeInViewportAsync(new LocatorAssertionsToBeInViewportOptions { Ratio = 1 });
    }
}
