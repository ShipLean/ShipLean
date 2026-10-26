using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Planning;

// The Planning view's sprint loads on the seeded Storefront project (SHIP), signed in as Joel. Only Sprint 24's
// capacity changes (in the first test); the second reads Sprint 26, which has none entered.
[TestFixture]
public class PlanningCapacityTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Planning_WhenOverCapacity_ShouldWarnNotBlock()
    {
        var put = await Page.Context.APIRequest.PutAsync($"/api/sprints/{SeedIds.Sprints.Sprint24}/capacity", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "arrange" },
            DataObject = new { members = new[] { new { userId = SeedIds.Users.Joel, points = 10, note = (string?)null } } },
        });
        put.Ok.ShouldBeTrue(await put.TextAsync());

        var planning = new PlanningBacklog(Page);
        await planning.OpenAsync("SHIP");

        await Expect(planning.CapacityBar("Sprint 24")).ToHaveAttributeAsync("data-tone", "over");
        await Expect(planning.Committed("Sprint 24")).ToContainTextAsync("/ 10 pts");
        await Expect(planning.Over("Sprint 24")).ToContainTextAsync("over");

        // Nothing is blocked: the page still takes a new item.
        await planning.NewItem.ClickAsync();
        await Expect(planning.AddRow).ToBeVisibleAsync();
    }

    [Test]
    [Order(2)]
    public async Task Planning_WhenNoCapacity_ShouldShowVelocityChip()
    {
        var planning = new PlanningBacklog(Page);
        await planning.OpenAsync("SHIP");

        await Expect(planning.VelocityChip("Sprint 26")).ToBeVisibleAsync();
        await Expect(planning.VelocityChip("Sprint 26")).ToContainTextAsync("velocity");
        // No capacity entered: the muted bar is drawn against velocity, beside the chip.
        await Expect(planning.CapacityBar("Sprint 26")).ToHaveAttributeAsync("data-tone", "velocity");
        await Expect(planning.CapacityBar("Sprint 25")).ToBeVisibleAsync();
        await Expect(planning.VelocityChip("Sprint 25")).ToHaveCountAsync(0);
    }
}
