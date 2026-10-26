using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.BoardSettings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.BoardSettings;

// Joel edits SHIP's Stories board in one Board settings session while Tom has the same board open in a page of his own.
[TestFixture]
public class BoardSettingsJourneyTests : SeededAcceptanceTest
{
    [Test]
    public async Task J3_BoardSettingsSave_UpdatesOpenBoard()
    {
        await using var tom = await SecondViewer.SignInAsTomAsync(Page, BaseUrl);
        var joelsBoard = new BoardScreen(Page);
        var tomsBoard = new BoardScreen(tom.Page);
        await tomsBoard.OpenAsync("SHIP", "stories");
        await joelsBoard.OpenAsync("SHIP", "stories");
        await Expect(joelsBoard.Count("Ready")).ToHaveTextAsync("6/8");
        await Expect(tomsBoard.Count("Ready")).ToHaveTextAsync("6/8");
        var styled = new Regex(".+");
        await Expect(joelsBoard.Card("SHIP-156")).Not.ToHaveAttributeAsync("data-rule", styled);

        await Page.GetByTestId(BoardTestIds.SettingsOpen).ClickAsync();
        var columns = new ColumnsPane(Page);
        await Expect(columns.Pane).ToBeVisibleAsync();
        await columns.Names.Nth(1).FillAsync("Queued");
        await joelsBoard.SettingsWipInput(0).FillAsync("9");

        await Page.GetByTestId($"{SettingsDialogTestIds.NavItem}-styles").ClickAsync();
        var styles = new StylesPane(Page);
        await Expect(styles.Rows).ToHaveCountAsync(2);
        await styles.Add.ClickAsync();
        await Expect(styles.Rows).ToHaveCountAsync(3);
        await styles.Names.Nth(2).FillAsync("Urgent");
        await styles.ClauseField(2).SelectOptionAsync(new SelectOptionValue { Label = "Priority" });
        await styles.ClauseValue(2).SelectOptionAsync(new SelectOptionValue { Label = "3" });
        await Expect(Page.GetByTestId(SettingsDialogTestIds.FooterDirty)).ToBeVisibleAsync();

        // Held until Save: neither board has changed.
        await Expect(joelsBoard.Count("Ready")).ToHaveTextAsync("6/8");
        await Expect(tomsBoard.Count("Ready")).ToHaveTextAsync("6/8");
        await Expect(tomsBoard.Card("SHIP-156")).Not.ToHaveAttributeAsync("data-rule", styled);

        var saved = await Page.RunAndWaitForApiAsync(
            $"/api/boards/{ShipLean.TestSupport.SeedIds.Boards.ShipStories}/settings",
            "PUT",
            () => Page.GetByTestId(SettingsDialogTestIds.Save).ClickAsync());
        saved.Ok.ShouldBeTrue();
        await Expect(Page.GetByTestId(ComponentTestIds.Toast)).ToContainTextAsync("Board settings saved");
        await Expect(Page.GetByTestId(ComponentTestIds.Dialog)).ToBeHiddenAsync();

        foreach (var board in new[] { joelsBoard, tomsBoard })
        {
            await Expect(board.Count("Queued")).ToHaveTextAsync("6/9");
            await Expect(board.Header("Ready")).ToHaveCountAsync(0);
            await Expect(board.Card("SHIP-156")).ToHaveAttributeAsync("data-rule", styled);
        }

        await tom.Page.ReloadAsync();
        await Expect(tomsBoard.Count("Queued")).ToHaveTextAsync("6/9");
        await Expect(tomsBoard.Card("SHIP-156")).ToHaveAttributeAsync("data-rule", styled);
    }
}
