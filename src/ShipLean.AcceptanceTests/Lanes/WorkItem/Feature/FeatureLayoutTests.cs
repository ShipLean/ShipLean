using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class FeatureLayoutTests : SeededAcceptanceTest
{
    [Test]
    public async Task Open_WhenFeature_ShouldShowFeatureRegions()
    {
        var screen = new WorkItemScreen(Page);
        var layout = new FeatureLayout(Page);

        await screen.OpenAsync("SHIP", "SHIP-101");

        await Expect(layout.FormInDropZone).ToBeVisibleAsync();
        await Expect(screen.Title).ToHaveValueAsync("Checkout v2");
        var tops = new List<float>();
        foreach (var region in layout.MainColumn)
        {
            await Expect(region).ToBeVisibleAsync();
            tops.Add((await region.BoundingBoxAsync())!.Y);
        }
        tops.ShouldBeInOrder(SortDirection.Ascending);
        foreach (var region in layout.Rail)
        {
            await Expect(region).ToBeVisibleAsync();
        }
        await Expect(layout.AddLink).ToBeAttachedAsync();
        await Expect(screen.Criteria).ToHaveCountAsync(0);
    }
}
