using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.WorkItem;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

// A story or bug in a feature's Stories and bugs section opens in the modal over the feature page. Reads only.
[TestFixture]
public class FeaturePeekTests : SeededAcceptanceTest
{
    [Test]
    public async Task FeaturePeek_WhenStoryClickedOnFeaturePage_ShouldOpenStoryOverPage()
    {
        var screen = new WorkItemScreen(Page);
        var strip = new FeatureStripPanel(Page);
        var peek = new ItemPeekDialog(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        const string key = "SHIP-141";
        var link = strip.ChildTitles.Filter(new() { HasText = "Pay with saved card" });

        await link.ClickAsync();

        await peek.WaitOpenAsync(key);
        await Expect(Page).ToHaveURLAsync(new Regex($@"/p/SHIP/items/SHIP-101\?item={key}$"));
        await Expect(strip.Slug).ToBeAttachedAsync();

        await Page.GoBackAsync();

        await peek.WaitClosedAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/items/SHIP-101$"));
        await Expect(strip.Slug).ToBeAttachedAsync();
    }
}
