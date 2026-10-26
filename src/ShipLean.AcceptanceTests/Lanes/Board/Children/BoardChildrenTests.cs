using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;

namespace ShipLean.AcceptanceTests.Lanes.Board.Children;

// The seeded Storefront features board: a Feature's stories, listed from its roll-up. Reads only.
[TestFixture]
public class BoardChildrenTests : SeededAcceptanceTest
{
    [Test]
    public async Task FeaturesBoard_WhenRollupClicked_ShouldListChildren()
    {
        var board = new BoardScreen(Page);
        var children = new BoardChildren(Page);
        await board.OpenAsync("SHIP", "features");
        await Expect(children.List).ToHaveCountAsync(0);

        await children.OpenAsync(board.Card("SHIP-101"), "S");

        await Expect(children.List).ToBeVisibleAsync();
        await Expect(children.Row("SHIP-141")).ToContainTextAsync("Active");
        await Expect(children.Add("User story")).ToBeVisibleAsync();
        await Expect(children.Add("Bug")).ToBeVisibleAsync();
    }
}
