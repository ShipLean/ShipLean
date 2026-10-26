using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Columns;

// The Columns pane of the Storefront stories board, from the seed: New, Ready, In progress, In review, Done, with
// cards in In progress. Only the last test saves a change that sticks, so the earlier ones still see the seeded order.
[TestFixture]
public class ColumnsPaneTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task BoardSettings_WhenStateUnmapped_ShouldWarnAndOfferFix()
    {
        var pane = new ColumnsPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.AllMapped).ToBeVisibleAsync();

        await pane.BugState(1).SelectOptionAsync(new SelectOptionValue { Label = "New" });

        await Expect(pane.Unmapped).ToContainTextAsync("Bug state Triaged isn't on any column");
        await Expect(pane.Fix).ToHaveTextAsync("Map Ready to Triaged for bugs");
        await pane.Fix.ClickAsync();
        await Expect(pane.Unmapped).ToHaveCountAsync(0);
        await Expect(pane.AllMapped).ToBeVisibleAsync();
    }

    [Test]
    [Order(2)]
    public async Task BoardSettings_WhenColumnHasCards_ShouldRefuseDelete()
    {
        var pane = new ColumnsPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Names.Nth(2)).ToHaveValueAsync("In progress");

        await pane.Delete(2).ClickAsync();

        await Expect(pane.Blocked).ToContainTextAsync("In progress has");
        await Expect(pane.Deleted).ToHaveCountAsync(0);
        await Expect(pane.Names.Nth(2)).ToHaveValueAsync("In progress");
    }

    [Test]
    [Order(3)]
    public async Task BoardSettings_WhenColumnReordered_ShouldKeepFirstAndLast()
    {
        var pane = new ColumnsPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Grip(0)).ToBeDisabledAsync();
        await Expect(pane.Delete(0)).ToBeDisabledAsync();

        await pane.Grip(1).FocusAsync();
        for (var press = 0; press < 4; press++)
        {
            await Page.Keyboard.PressAsync("Alt+ArrowDown");
        }
        await pane.SaveAndWaitAsync();

        await Page.GotoAsync(Links.BoardSettings("SHIP", "stories", "columns"));
        string[] expected = ["New", "In progress", "In review", "Ready", "Done"];
        for (var row = 0; row < expected.Length; row++)
        {
            await Expect(pane.Names.Nth(row)).ToHaveValueAsync(expected[row]);
        }
    }
}
