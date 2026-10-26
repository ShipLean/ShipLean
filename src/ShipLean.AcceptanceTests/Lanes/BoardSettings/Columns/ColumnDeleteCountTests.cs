using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Columns;

// The Columns pane's delete guard on the Storefront stories board, whose In progress column holds cards.
[TestFixture]
public class ColumnDeleteCountTests : SeededAcceptanceTest
{
    [Test]
    public async Task BoardSettings_WhenColumnHasCards_ShouldSayHowMany()
    {
        var pane = new ColumnsPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Names.Nth(2)).ToHaveValueAsync("In progress");
        await Expect(pane.Rows).ToHaveCountAsync(5);

        await pane.Delete(2).ClickAsync();

        await Expect(pane.Blocked).ToContainTextAsync("In progress has");
        await Expect(pane.Blocked).ToContainTextAsync(new Regex(@"has [1-9]\d* cards?, so it can't be deleted yet"));
        await Expect(pane.Rows).ToHaveCountAsync(5);
    }
}
