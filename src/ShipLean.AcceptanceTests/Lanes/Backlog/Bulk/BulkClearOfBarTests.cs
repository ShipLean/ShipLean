using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// A long backlog with the action bar showing: the list scrolls under the bar, so the last row can be scrolled clear of
// it and the bar is still in the window with the scroller back at the top. (BulkInViewTests holds the bar and the
// report in the window; the class has rows of its own because each class shares one database.)
[TestFixture]
public class BulkClearOfBarTests : SeededAcceptanceTest
{
    // The last row's gap above the bar once the scroller is at its end, and the bar's reach below the window at the top.
    private const string Clearance = """
        ([rowId, barId, mainId]) => {
          const main = document.querySelector(`[data-testid="${mainId}"]`);
          const rows = [...document.querySelectorAll(`[data-testid="${rowId}"]`)];
          const bar = document.querySelector(`[data-testid="${barId}"]`);
          main.scrollTop = main.scrollHeight;
          const gap = bar.getBoundingClientRect().top - rows[rows.length - 1].getBoundingClientRect().bottom;
          main.scrollTop = 0;
          return { gap, barBottomAtTop: bar.getBoundingClientRect().bottom - window.innerHeight, scrolls: main.scrollHeight - main.clientHeight };
        }
        """;

    [TestCase(1280, "SHIP-156", "SHIP-158", "SHIP-160")]
    [TestCase(1440, "SHIP-162", "SHIP-163", "SHIP-165")]
    public async Task Bulk_WhenListScrolledToEnd_ShouldLeaveLastRowClearOfBar(int width, string first, string changed, string last)
    {
        await BulkLayout.ShowAsync(Page, BaseUrl, width, first, changed, last);

        var reach = await Page.EvaluateAsync<JsonElement>(
            Clearance, new object[] { BacklogTestIds.Row, BacklogTestIds.BulkBar, AppTestIds.ShellMain });

        reach.GetProperty("scrolls").GetDouble().ShouldBeGreaterThan(0);
        reach.GetProperty("gap").GetDouble().ShouldBeGreaterThanOrEqualTo(-0.5);
        reach.GetProperty("barBottomAtTop").GetDouble().ShouldBeLessThanOrEqualTo(0.5);
    }
}
