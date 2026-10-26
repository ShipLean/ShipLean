using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Capacity.CapacityApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Capacity;

// Nothing is saved here: the figures move as the member types.
[TestFixture]
public class CapacityWarningTests : SeededAcceptanceTest
{
    [Test]
    public async Task Capacity_WhenFarFromVelocity_ShouldWarn()
    {
        var velocity = await VelocityAsync(Page, "SHIP");
        velocity.ShouldBeGreaterThan(10);
        var member = (await MemberNamesAsync(Page, "SHIP"))[0];
        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(capacity.KpiVelocity).ToHaveTextAsync($"{velocity:0.##}");

        await capacity.Clear.ClickAsync();
        await Expect(capacity.Callout).ToHaveAttributeAsync("data-tone", "info");
        await Expect(capacity.Callout).ToContainTextAsync("No capacity entered");

        await capacity.Points(member).FillAsync($"{velocity:0.##}");
        await Expect(capacity.Callout).ToHaveAttributeAsync("data-tone", "info");
        await Expect(capacity.Callout).ToContainTextAsync("In line with velocity");

        await capacity.Points(member).FillAsync($"{velocity * 2:0.##}");
        await Expect(capacity.Callout).ToHaveAttributeAsync("data-tone", "warn");
        await Expect(capacity.Callout).ToContainTextAsync("100% above velocity");

        await capacity.Points(member).FillAsync($"{Math.Floor(velocity / 2):0.##}");
        await Expect(capacity.Callout).ToHaveAttributeAsync("data-tone", "warn");
        await Expect(capacity.Callout).ToContainTextAsync("below velocity");
    }
}
