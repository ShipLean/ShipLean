using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// The range-to-window rule (SHIP-1398 K3). Two-week sprints from Mon 29 Jun 2026; today is Sat 24 Oct 2026, inside Sprint 24
// (the design's sample: Sprint 16 ends Fri 10 Jul, Sprint 24 ends Fri 30 Oct, Sprint 25 starts 2 Nov).
[TestFixture]
public class InspectionWindowTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    private static readonly IReadOnlyList<InspectionSprint> Sprints =
    [
        .. Enumerable.Range(16, 11).Select(n =>
        {
            var start = new DateOnly(2026, 6, 29).AddDays((n - 16) * 14);
            return new InspectionSprint(Guid.NewGuid(), $"Sprint {n}", start, start.AddDays(11));
        }),
    ];

    private static string[] Names(InspectionWindow window) => [.. window.Shown.Select(s => s.Name)];

    // Mutation: start at the first shown sprint instead of the quarter before the one holding it.
    [Test]
    public void Window_WhenLast6_ShouldStartAtQuarterBeforeFirstShownSprint()
    {
        var window = InspectionWindow.For(InspectionRange.Last6, Sprints, Today);

        Names(window).ShouldBe(["Sprint 19", "Sprint 20", "Sprint 21", "Sprint 22", "Sprint 23", "Sprint 24"]);
        // Sprint 19 ends Fri 21 Aug, in Q3; the quarter before starts 1 Apr.
        window.From.ShouldBe(new DateOnly(2026, 4, 1));
        window.To.ShouldBe(Today);
    }

    [Test]
    public void Window_WhenAll_ShouldStartAtFirstSprint()
    {
        var window = InspectionWindow.For(InspectionRange.All, Sprints, Today);

        window.Shown.Count.ShouldBe(9);
        window.From.ShouldBe(new DateOnly(2026, 6, 29));
        window.To.ShouldBe(Today);
    }

    [Test]
    public void Window_WhenQuarter_ShouldShowSprintsEndingThisQuarter()
    {
        var window = InspectionWindow.For(InspectionRange.Quarter, Sprints, Today);

        Names(window).ShouldBe(["Sprint 22", "Sprint 23", "Sprint 24"]);
        window.From.ShouldBe(new DateOnly(2026, 7, 1));
    }

    [Test]
    public void Window_WhenLastQuarter_ShouldShowSprintsEndingLastQuarter()
    {
        var window = InspectionWindow.For(InspectionRange.LastQuarter, Sprints, Today);

        Names(window).ShouldBe(["Sprint 16", "Sprint 17", "Sprint 18", "Sprint 19", "Sprint 20", "Sprint 21"]);
        window.From.ShouldBe(new DateOnly(2026, 4, 1));
    }

    [Test]
    public void Window_WhenPeriodBeforeRange_ShouldWidenFromToItsQuarterBefore()
    {
        var window = InspectionWindow.For(InspectionRange.Quarter, Sprints, Today, Sprints[0]);

        window.From.ShouldBe(new DateOnly(2026, 4, 1));
    }

    [Test]
    public void Window_WhenNoSprints_ShouldShowNothingFromTheQuarterBeforeToday()
    {
        var window = InspectionWindow.For(InspectionRange.Last6, [], Today);

        window.Shown.ShouldBeEmpty();
        window.From.ShouldBe(new DateOnly(2026, 7, 1));
    }
}
