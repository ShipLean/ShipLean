using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Styles;

// The Styles pane of the Storefront stories board, from the seed. The test only edits the draft: nothing is saved.
[TestFixture]
public class StylesPaneTests : SeededAcceptanceTest
{
    [Test]
    public async Task BoardSettings_WhenTwoRulesMatch_ShouldPreviewFirst()
    {
        var pane = new StylesPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Rows).ToHaveCountAsync(2);
        await Expect(pane.PreviewCaptions.First).ToContainTextAsync("Blocked");

        await pane.Add.ClickAsync();
        await Expect(pane.Rows).ToHaveCountAsync(3);
        await pane.Names.Nth(2).FillAsync("Urgent");
        await pane.ClauseField(2).SelectOptionAsync(new SelectOptionValue { Label = "Priority" });
        await pane.ClauseValue(2).SelectOptionAsync(new SelectOptionValue { Label = "1" });

        await Expect(pane.PreviewCaptions.First).ToHaveTextAsync("Blocked wins. Also matches Urgent");

        await pane.Grip(2).FocusAsync();
        await Page.Keyboard.PressAsync("Alt+ArrowUp");
        await Page.Keyboard.PressAsync("Alt+ArrowUp");

        await Expect(pane.Names.First).ToHaveValueAsync("Urgent");
        await Expect(pane.PreviewCaptions.First).ToHaveTextAsync("Urgent wins. Also matches Blocked");
    }
}
