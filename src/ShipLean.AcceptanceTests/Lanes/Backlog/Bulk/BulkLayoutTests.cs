using ShipLean.AcceptanceTests.Harness;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// The Planning page with the action bar, the changed-rows report and the Undo toast showing, at the widths the app is used at.
[TestFixture]
public class BulkLayoutTests : SeededAcceptanceTest
{
    // Each case has rows of its own: the class shares one database.
    [TestCase(1280, "SHIP-156", "SHIP-158", "SHIP-160")]
    [TestCase(1440, "SHIP-162", "SHIP-163", "SHIP-165")]
    public async Task Bulk_WhenBarAndReportShow_ShouldNotOverflowSideways(int width, string first, string changed, string last)
    {
        var reach = await BulkLayout.ShowAsync(Page, BaseUrl, width, first, changed, last);

        reach.GetProperty("page").GetInt32().ShouldBeLessThanOrEqualTo(0);
        reach.GetProperty("barSide").GetDouble().ShouldBeLessThanOrEqualTo(0.5);
        reach.GetProperty("changedSide").GetDouble().ShouldBeLessThanOrEqualTo(0.5);
    }
}
