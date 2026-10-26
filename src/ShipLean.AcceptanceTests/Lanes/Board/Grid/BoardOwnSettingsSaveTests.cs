using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.Board.Grid;

// Saving the board's settings in this tab updates the board under the dialog (the tab's own event is dropped). The class
// saves settings, so it has its own database.
[TestFixture]
public class BoardOwnSettingsSaveTests : SeededAcceptanceTest
{
    [Test]
    public async Task Board_WhenWipEditedOverTheBoard_ShouldShowTheNewLimitAfterTheDialogCloses()
    {
        var board = new BoardScreen(Page);
        await Page.GotoAsync(Links.BoardSettings("SHIP", "stories", "columns"));
        var columns = new ColumnsPane(Page);
        await Expect(columns.Pane).ToBeVisibleAsync();
        await Expect(board.Count("Ready")).ToHaveTextAsync("6/8");

        // The first column with a limit input is Ready.
        await board.SettingsWipInput(0).FillAsync("9");
        await columns.SaveAndWaitAsync();

        await Expect(board.Count("Ready")).ToHaveTextAsync("6/9");
    }
}
