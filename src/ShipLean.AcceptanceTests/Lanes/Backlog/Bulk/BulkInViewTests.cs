using ShipLean.AcceptanceTests.Harness;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// Red until the bar and the report stay in the window. Today they sit at the end of the page's flow inside the shell's
// scroller (ProjectScreen's block div.p-4, so the screen is as tall as its rows, not the window), and a backlog taller
// than the window pushes them below the fold. Owner: S2 for the bar, S3 for the report, with the screens' mounts (A0r).
[TestFixture]
public class BulkInViewTests : SeededAcceptanceTest
{
    [TestCase(1280, "SHIP-156", "SHIP-158", "SHIP-160")]
    [TestCase(1440, "SHIP-162", "SHIP-163", "SHIP-165")]
    public async Task Bulk_WhenBarAndReportShow_ShouldStayInView(int width, string first, string changed, string last)
    {
        var reach = await BulkLayout.ShowAsync(Page, BaseUrl, width, first, changed, last);

        reach.GetProperty("barBottom").GetDouble().ShouldBeLessThanOrEqualTo(0.5);
        reach.GetProperty("changedBottom").GetDouble().ShouldBeLessThanOrEqualTo(0.5);
    }
}
