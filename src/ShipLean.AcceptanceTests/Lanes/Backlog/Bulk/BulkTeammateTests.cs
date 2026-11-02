using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// A teammate's many-row move arriving over the hub while Planning is open.
[TestFixture]
public class BulkTeammateTests : SeededAcceptanceTest
{
    [Test]
    public async Task Bulk_WhenTeammateMovesTwentyRows_ShouldShowOneToast()
    {
        var planning = new PlanningBacklog(Page);
        var bar = new BulkBar(Page);
        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        var keys = await TeammateBulk.AddStoriesAsync(tom, SeedIds.Projects.Ship, 20, "Teammate bulk");
        await planning.OpenAsync("SHIP");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var sprint26 = SeedIds.Sprints.Sprint26.ToString();
        await Expect(bar.Row(keys[^1])).ToHaveAttributeAsync("data-section", "backlog");

        var moved = await TeammateBulk.MoveToSprintAsync(tom, SeedIds.Projects.Ship, SeedIds.Sprints.Sprint26, keys);

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await bar.WaitForSectionAsync(sprint26, (await BulkReadBack.InOrderAsync(Page.Context.APIRequest, keys.ToArray())));
        var teammateToasts = bar.Toasts.Filter(new() { HasTextString = "20 items changed by a teammate" });
        await Expect(teammateToasts).ToHaveCountAsync(1);
    }
}
