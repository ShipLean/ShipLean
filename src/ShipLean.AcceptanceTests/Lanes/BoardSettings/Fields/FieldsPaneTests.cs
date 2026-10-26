using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Fields;

// The Fields pane of the Storefront stories board, from the seed. The test only toggles the draft: nothing is saved.
[TestFixture]
public class FieldsPaneTests : SeededAcceptanceTest
{
    [Test]
    public async Task BoardSettings_WhenFieldToggled_ShouldUpdatePreview()
    {
        var pane = new FieldsPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.PreviewFields).ToHaveCountAsync(1);
        await Expect(pane.Preview).ToContainTextAsync("KEY-58");

        await pane.ShowKey.UncheckAsync();
        await pane.Add.SelectOptionAsync(new SelectOptionValue { Label = "Changed by" });

        await Expect(pane.Preview).Not.ToContainTextAsync("KEY-58");
        await Expect(pane.PreviewFields).ToHaveCountAsync(2);
        await Expect(pane.PreviewFields.Last).ToContainTextAsync("Changed by");
        await Expect(pane.ExtraCount).ToContainTextAsync("4 / 10");
    }
}
