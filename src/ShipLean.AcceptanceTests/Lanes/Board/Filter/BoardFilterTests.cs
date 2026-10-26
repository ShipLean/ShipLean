using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;

namespace ShipLean.AcceptanceTests.Lanes.Board.Filter;

// The seeded Storefront stories board, filtered by keyword. Nothing is written, so the class shares one database.
[TestFixture]
public class BoardFilterTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenFiltered_ShouldKeepWipCountsAndSaySo()
    {
        var board = new BoardScreen(Page);
        var filter = new BoardFilter(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Card("SHIP-141")).ToBeVisibleAsync();
        await Expect(board.Card("SHIP-144")).ToBeVisibleAsync();
        var readyBefore = await board.Count("Ready").InnerTextAsync();
        await Expect(filter.Note).ToHaveCountAsync(0);

        await filter.FilterByKeywordAsync("SHIP-141");

        await Expect(board.Card("SHIP-144")).ToHaveCountAsync(0);
        await Expect(board.Card("SHIP-141")).ToBeVisibleAsync();
        await Expect(filter.Note).ToContainTextAsync("hidden by the filter");
        await Expect(filter.Note).ToContainTextAsync("WIP counts include hidden cards");
        await Expect(board.Count("Ready")).ToHaveTextAsync(readyBefore);

        await filter.Clear.ClickAsync();

        await Expect(board.Card("SHIP-144")).ToBeVisibleAsync();
        await Expect(filter.Note).ToHaveCountAsync(0);
    }
}
