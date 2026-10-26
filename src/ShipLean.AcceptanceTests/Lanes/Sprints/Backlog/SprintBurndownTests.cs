using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// The app's clock is pinned to 12:00 UTC on the latest weekday on or before the run date, so the close lands on a working
// day and moves the burndown's last point whichever day of the week this runs.
[TestFixture]
public class SprintBurndownTests : SeededAcceptanceTest
{
    private static DateOnly LatestWeekday()
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        return today.DayOfWeek switch { DayOfWeek.Saturday => today.AddDays(-1), DayOfWeek.Sunday => today.AddDays(-2), _ => today };
    }

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(LatestWeekday().ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)));
    };

    [Test]
    public async Task SprintBacklog_WhenStoryClosedViaApi_ShouldUpdateBurndown()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", sprint);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var before = await ReadAsync(Page, $"/api/sprints/{sprint}/metrics");
        var pointsBefore = before.GetProperty("burndown").GetArrayLength();
        var lastBefore = before.GetProperty("burndown").EnumerateArray().Last().GetProperty("remaining").GetDecimal();
        await Expect(backlog.BurndownPoints).ToHaveCountAsync(pointsBefore);
        await Expect(backlog.BurndownPoints.Last).ToHaveAttributeAsync("data-remaining", Pts(lastBefore));

        var story = await ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-144");
        var points = story.GetProperty("points").GetDecimal();
        await SetStateAsync(Page, "SHIP-144", SeedIds.States.Ship.StoryClosed);

        await Expect(backlog.BurndownPoints.Last).ToHaveAttributeAsync("data-remaining", Pts(lastBefore - points));
        await Expect(backlog.BurndownRemaining).ToHaveTextAsync($"{Pts(before.GetProperty("remaining").GetDecimal() - points)} pts left");
        await Expect(backlog.BurndownPoints).ToHaveCountAsync(pointsBefore);
    }
}
