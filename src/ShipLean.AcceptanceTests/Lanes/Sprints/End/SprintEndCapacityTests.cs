using System.Globalization;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.End;

// Sprint 24 ended; Sprint 25's team capacity is arranged to be one point short of taking everything.
[TestFixture]
public class SprintEndCapacityTests : SeededAcceptanceTest
{
    private static string Pts(decimal points) => points.ToString("0.##", CultureInfo.InvariantCulture);

    [Test]
    public async Task SprintEnd_WhenNextSprintOverCapacity_ShouldWarn()
    {
        await EndSprint24Async(ConnectionString);
        var rollover = await ReadRolloverAsync(Page, SeedIds.Sprints.Sprint24);
        var planned = rollover.GetProperty("nextSprintPoints").GetDecimal();
        var moving = Points(rollover);
        var capacity = planned + moving - 1;
        await SetTeamCapacityAsync(ConnectionString, SeedIds.Sprints.Sprint25, SeedIds.SprintCapacities.Sprint25Priya, capacity);
        var first = Keys(rollover)[0];
        var firstPoints = rollover.GetProperty("items")[0].GetProperty("points").GetDecimal();
        firstPoints.ShouldBeGreaterThan(0);

        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync("SHIP");

        await Expect(prompt.After).ToContainTextAsync($"Sprint 25 after this: {Pts(planned + moving)} / {Pts(capacity)} pts");
        await Expect(prompt.After).ToContainTextAsync("1 over team capacity");
        await Expect(prompt.After).ToHaveAttributeAsync("data-over", "true");
        await Expect(prompt.Apply).ToBeEnabledAsync();

        await prompt.DestinationOf(first).SelectOptionAsync("backlog");

        await Expect(prompt.After).ToContainTextAsync($"Sprint 25 after this: {Pts(planned + moving - firstPoints)} / {Pts(capacity)} pts");
        await Expect(prompt.After).ToHaveAttributeAsync("data-over", "false");
    }
}
