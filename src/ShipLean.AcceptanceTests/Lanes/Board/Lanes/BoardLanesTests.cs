using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Board.Lanes;

// The lane choice is the signed-in user's own: it writes a preference, so the class has a database of its own.
[TestFixture]
public class BoardLanesTests : SeededAcceptanceTest
{
    [Test]
    public async Task FeaturesBoard_WhenLaneChosen_ShouldPersistPerUser()
    {
        var board = new BoardScreen(Page);
        var lanes = new BoardLanes(Page);
        await board.OpenAsync("SHIP", "features");
        await Expect(board.Card("SHIP-101")).ToBeVisibleAsync();
        await Expect(lanes.Option("Swimlanes")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(lanes.Lanes).ToHaveCountAsync(2);
        await Expect(BoardScreen.LaneName(lanes.Lanes.First)).ToHaveTextAsync("Expedite");

        await lanes.ChooseAsync("None");
        await Expect(lanes.Lanes).ToHaveCountAsync(1);

        await lanes.ChooseAsync("Swimlanes");
        await Page.ReloadAsync();
        await Expect(lanes.Option("Swimlanes")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(BoardScreen.LaneName(lanes.Lanes.First)).ToHaveTextAsync("Expedite");

        await lanes.ChooseAsync("By Epic");

        await Expect(lanes.Lanes.Nth(1)).ToBeVisibleAsync();
        await Expect(BoardScreen.LaneName(lanes.Lanes.Last)).ToHaveTextAsync("No Epic");
        await Page.ReloadAsync();
        await Expect(lanes.Option("By Epic")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(lanes.Lanes.Nth(1)).ToBeVisibleAsync();
        (await BoardLanes.LanesSeenByTomAsync(Page, BaseUrl, SeedIds.Boards.ShipFeatures)).ShouldBe(2);
    }
}
