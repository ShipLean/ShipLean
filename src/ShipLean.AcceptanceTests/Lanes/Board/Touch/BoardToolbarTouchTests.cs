using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Board.Touch;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.Touch;

// The seeded Storefront Stories board on a phone (SHIP-719 D5): the one-row toolbar with its meta line, the Filter sheet
// and the ⓘ sheet. The touch rules are checked on this task's regions only (the toolbar and its sheets). Reads only.
[TestFixture]
public class BoardToolbarTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private ILocator Meta => Page.GetByTestId(BoardTestIds.Meta);

    private async Task<(BoardScreen Board, TouchShell Shell)> OpenAsync()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await new BoardPager(Page).WaitAsync();
        return (board, new TouchShell(Page));
    }

    [Test]
    public async Task Filter_WhenPersonPickedOnPhone_ShouldShowOnlyTheirCardsAndCount()
    {
        var (_, shell) = await OpenAsync();
        var cards = Page.GetByTestId(BoardTestIds.Card);
        var all = await cards.CountAsync();
        all.ShouldBeGreaterThan(1);
        await Expect(Meta).Not.ToContainTextAsync("Filtered");
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(BoardTestIds.Toolbar))).ShouldBeEmpty();

        await Page.GetByTestId(BoardTestIds.FilterToggle).TapAsync();
        var sheet = shell.Sheet("Filter");
        await Expect(sheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();
        var read = await Page.RunAndWaitForResponseAsync(
            () => sheet.GetByTestId(BoardTestIds.FilterPerson).Filter(new LocatorFilterOptions { HasText = "Tom Okafor" }).TapAsync(),
            r => r.Request.Method == "GET" && r.Url.Contains("/api/boards/") && r.Url.Contains("assignee="));
        read.Ok.ShouldBeTrue();

        await Expect(Meta).ToContainTextAsync(new Regex(@"Filtered: \d+ of \d+ cards"));
        var text = await Meta.InnerTextAsync();
        var match = Regex.Match(text, @"Filtered: (\d+) of (\d+) cards");
        var shown = int.Parse(match.Groups[1].Value);
        int.Parse(match.Groups[2].Value).ShouldBeGreaterThan(shown);
        shown.ShouldBeGreaterThan(0);
        await Expect(Page.GetByTestId(BoardTestIds.FilterToggle)).ToHaveTextAsync("Filter (1)");
        // Done shows only its newest cards, so the count on screen can be under the "shown" the meta line gives.
        var count = await cards.CountAsync();
        count.ShouldBeInRange(1, shown);
        for (var i = 0; i < count; i++)
        {
            await Expect(BoardMoves.Assignee(cards.Nth(i)).First).ToHaveAttributeAsync("title", "Tom Okafor");
        }

        await sheet.GetByTestId(BoardTestIds.FilterClear).TapAsync();

        await Expect(Meta).Not.ToContainTextAsync("Filtered");
        await Expect(cards).ToHaveCountAsync(all);
        await shell.CloseSheetAsync();
        await Expect(shell.Sheets).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(BoardTestIds.FilterToggle)).ToHaveTextAsync("Filter");
    }

    [Test]
    public async Task Dod_WhenInfoTappedOnPhone_ShouldOpenSheet()
    {
        var (board, shell) = await OpenAsync();
        var header = board.Headers.First;
        var name = await header.GetByTestId(BoardTestIds.ColumnName).InnerTextAsync();

        await BoardDod.Open(header).TapAsync();

        var sheet = shell.Sheet(name);
        await Expect(sheet).ToBeVisibleAsync();
        await Expect(sheet.GetByTestId(BoardTestIds.DodType).First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(BoardTestIds.Dod)).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();

        await shell.CloseSheetAsync();

        await Expect(shell.Sheets).ToHaveCountAsync(0);
    }
}
