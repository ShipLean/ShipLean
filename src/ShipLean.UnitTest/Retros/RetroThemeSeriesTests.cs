using ShipLean.Application.Features.Retros.Store;
using ShipLean.Contracts.Retros;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroThemeSeriesTests
{
    private static readonly DateOnly FirstMonday = new(2026, 3, 2);

    [TestCase(2, 0, TestName = "ClosingIndex_WhenDayInsideFirstSprint_ShouldBeZero")]
    [TestCase(9, 1, TestName = "ClosingIndex_WhenDayOnSecondSprintFirstDay_ShouldBeOne")]
    [TestCase(-3, -1, TestName = "ClosingIndex_WhenDayBeforeEverySprint_ShouldBeMinusOne")]
    [TestCase(30, 1, TestName = "ClosingIndex_WhenDayAfterTheLastSprint_ShouldBeTheLast")]
    public void ClosingIndex_ShouldBeTheSprintContainingTheDay(int dayOffset, int expected)
    {
        var series = Series(2);

        RetroThemeSeries.ClosingIndex(series, FirstMonday.AddDays(dayOffset - 2)).ShouldBe(expected);
    }

    [Test]
    public void ClosingIndex_WhenDayFallsInAGapBetweenSprints_ShouldBeTheLatestSprintThatStartedBefore()
    {
        List<RetroSeriesSprint> series =
        [
            new(Guid.NewGuid(), FirstMonday, FirstMonday.AddDays(4), true),
            new(Guid.NewGuid(), FirstMonday.AddDays(14), FirstMonday.AddDays(18), true),
        ];

        RetroThemeSeries.ClosingIndex(series, FirstMonday.AddDays(9)).ShouldBe(0);
    }

    [Test]
    public void Build_WhenGoodToneTheme_ShouldCountWentWellAndNeverRecur()
    {
        var series = Series(4);
        var theme = Guid.NewGuid();
        var points = series.Select(sprint => (theme, sprint.Id, RetroCategory.WentWell));

        var entry = RetroThemeSeries.Build(series, points, new Dictionary<Guid, RetroTone> { [theme] = RetroTone.Good }, [], new HashSet<Guid>()).Single().Value;

        entry.Counts.ShouldBe([1, 1, 1, 1]);
        entry.IsRecurring.ShouldBeFalse();
    }

    [Test]
    public void Build_WhenHelpedFixClosedBeforeAnySprint_ShouldGiveItNoVerdictAndStillRecur()
    {
        var series = Series(4);
        var theme = Guid.NewGuid();
        var points = series.Select(sprint => (theme, sprint.Id, RetroCategory.WentBadly));
        var closed = new[] { new RetroSeriesClosed(Guid.NewGuid(), theme, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)) };

        var entry = RetroThemeSeries.Build(series, points, new Dictionary<Guid, RetroTone> { [theme] = RetroTone.Bad }, closed, new HashSet<Guid>()).Single().Value;

        entry.Closed.ShouldBeEmpty();
        entry.IsRecurring.ShouldBeTrue();
        entry.Status.ShouldBe(RetroThemeStatus.Recurring);
    }

    private static List<RetroSeriesSprint> Series(int count)
        => [.. Enumerable.Range(0, count).Select(i => new RetroSeriesSprint(Guid.NewGuid(), FirstMonday.AddDays(7 * i), FirstMonday.AddDays((7 * i) + 6), true))];
}
