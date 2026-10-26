using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Board.Grid;

// A teammate's settings change reaches the open board. The class saves settings, so it has its own database.
[TestFixture]
public class BoardSettingsFlashTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenSettingsChangeElsewhere_ShouldFlashHeader()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.Headers).ToHaveCountAsync(5);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(board.Header("Ready")).ToHaveAttributeAsync("data-flashes", "0");
        await Expect(board.Count("Ready")).ToHaveTextAsync("6/8");

        await BoardScreen.ChangeSettingsAsync(Page.Context, SeedIds.Boards.ShipStories, settings => BoardScreen.SetWipLimit(settings, "Ready", 9));

        await Expect(board.Header("Ready")).ToHaveAttributeAsync("data-flashes", "1");
        await Expect(board.Header("In progress")).ToHaveAttributeAsync("data-flashes", "1");
        // The board refetched with the new limit.
        await Expect(board.Count("Ready")).ToContainTextAsync("/9");
    }
}
