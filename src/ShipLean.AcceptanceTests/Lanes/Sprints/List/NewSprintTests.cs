using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.List;

// The New sprint rail on the seeded Storefront project (SHIP, Sprints 19 to 26 with weekdays as working days), signed in
// as Joel. The tests run in order: the create comes before the overlap, which needs a sprint to run into.
[TestFixture]
public class NewSprintTests : SeededAcceptanceTest
{
    private static bool Weekday(DateOnly day) => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    [Test]
    [Order(1)]
    public async Task Sprints_WhenNewSprintCreated_ShouldDefaultDatesAndLength()
    {
        var last = await SprintDatabase.ReadDatesAsync(ConnectionString, SeedIds.Sprints.Sprint26);
        var expectedStart = last.End.AddDays(1);
        while (!Weekday(expectedStart))
        {
            expectedStart = expectedStart.AddDays(1);
        }

        // The same calendar length as the last sprint. Only the start moves onto a working day; the end is not clipped,
        // so a sprint that follows a Sunday end lands on a Sunday too.
        var expectedEnd = expectedStart.AddDays(last.End.DayNumber - last.Start.DayNumber);

        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");

        await Expect(sprints.Name).ToHaveValueAsync("Sprint 27");
        await Expect(sprints.Start).ToHaveValueAsync(expectedStart.ToString("yyyy-MM-dd"));
        await Expect(sprints.End).ToHaveValueAsync(expectedEnd.ToString("yyyy-MM-dd"));
        (expectedEnd.DayNumber - expectedStart.DayNumber).ShouldBe(last.End.DayNumber - last.Start.DayNumber);

        var created = await sprints.CreateAsync(SeedIds.Projects.Ship.ToString());
        created.Ok.ShouldBeTrue();

        await Expect(sprints.RowOf("Sprint 27")).ToBeVisibleAsync();
        // Current Sprint 24, then the future in date order: Sprints 25, 26 and the new 27.
        await Expect(sprints.Rows.Nth(3)).ToContainTextAsync("Sprint 27");
        var saved = await SprintDatabase.ReadDatesByNameAsync(ConnectionString, "SHIP", "Sprint 27");
        saved.ShouldBe(new SprintDatabase.Dates(expectedStart, expectedEnd));
        await Expect(sprints.Name).ToHaveValueAsync("Sprint 28");
        var followingStart = expectedEnd.AddDays(1);
        while (!Weekday(followingStart))
        {
            followingStart = followingStart.AddDays(1);
        }

        await Expect(sprints.Start).ToHaveValueAsync(followingStart.ToString("yyyy-MM-dd"));
    }

    [Test]
    [Order(2)]
    public async Task Sprints_WhenOverlapping_ShouldRefuseInline()
    {
        var running = await SprintDatabase.ReadDatesAsync(ConnectionString, SeedIds.Sprints.Sprint25);
        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");
        var before = await sprints.Rows.CountAsync();

        await sprints.Start.FillAsync(running.End.ToString("yyyy-MM-dd"));
        var refused = await sprints.CreateAsync(SeedIds.Projects.Ship.ToString());

        refused.Status.ShouldBe(400);
        await Expect(sprints.Error).ToHaveTextAsync("Sprints cannot overlap.");
        await Expect(sprints.Start).ToHaveValueAsync(running.End.ToString("yyyy-MM-dd"));
        await Expect(sprints.Rows).ToHaveCountAsync(before);
    }

    [Test]
    [Order(3)]
    public async Task Sprints_WhenProjectNamesByQuarter_ShouldSuggestYearQuarterAndNumberAndFollowTheDates()
    {
        await SprintDatabase.UseYearQuarterNamingAsync(ConnectionString, "SHIP");
        var sprints = new AllSprints(Page);
        await sprints.OpenAsync("SHIP");

        await Expect(sprints.Name).ToHaveValueAsync(new Regex(@"^\d{4} Q[1-4] Sprint \d+$"));

        // The name follows the dates while it is untouched: a start on the quarter's fourth day, two weeks long.
        await sprints.End.FillAsync("2031-01-17");
        await sprints.Start.FillAsync("2031-01-04");
        await Expect(sprints.Name).ToHaveValueAsync("2031 Q1 Sprint 1");

        await sprints.Name.FillAsync("Hardening");
        await sprints.Start.FillAsync("2031-04-05");
        await sprints.End.FillAsync("2031-04-18");
        await Expect(sprints.Name).ToHaveValueAsync("Hardening");
    }
}
