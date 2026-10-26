using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Planning;

// The forecast lines and the velocity override on the seeded Storefront project (SHIP), signed in as Joel. Its own
// class: the override and the Forecast toggle are saved for the project and the user.
[TestFixture]
public class PlanningForecastTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Planning_WhenOverrideSet_ShouldMoveForecastLines()
    {
        var planning = new PlanningBacklog(Page);
        await planning.OpenAsync("SHIP");
        await Expect(planning.ForecastLines.First).ToBeVisibleAsync();
        var before = await planning.LineKeysAsync();

        var saved = await planning.SetOverrideAsync(1000);

        saved.Status.ShouldBe(204);
        await Expect(planning.Velocity).ToContainTextAsync("1000");
        await Expect(planning.Velocity).ToContainTextAsync("(override)");
        var after = await planning.LineKeysAsync();
        // Sprint 25 keeps its capacity-based room, and Sprint 26 takes everything left at 1000: the middle line goes.
        after.ShouldBe(new[] { before[0], before[^1] });
    }

    [Test]
    [Order(2)]
    public async Task Planning_WhenForecastToggledOff_ShouldHideLinesAfterReload()
    {
        var planning = new PlanningBacklog(Page);
        await planning.OpenAsync("SHIP");
        await Expect(planning.ForecastLines.First).ToBeVisibleAsync();

        var saved = await Page.RunAndWaitForApiAsync("/api/me/preferences/view%3Abacklog", "PUT", () => planning.ForecastToggle.ClickAsync());

        saved.Status.ShouldBe(204);
        await Expect(planning.ForecastLines).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await Expect(planning.Section("Backlog")).ToBeVisibleAsync();
        await Expect(planning.ForecastToggle).Not.ToBeCheckedAsync();
        await Expect(planning.ForecastLines).ToHaveCountAsync(0);
    }
}
