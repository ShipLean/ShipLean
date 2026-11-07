using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Backups.Cron;
using ShipLean.UnitTest.Persisted;

namespace ShipLean.UnitTest.Backups.Cron;

// The backup schedule's cron rules (decision D2): 5 fields, UTC, at least an hour apart over the next 400 days.
[TestFixture]
public class CronScheduleTests
{
    // The instant the pinned backup-cron-preview.json examples are computed against.
    private static readonly DateTimeOffset PinnedNow = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);

    private static CronSchedule Schedule() => new(new FakeTimeProvider(PinnedNow));

    [Test]
    public void Preview_WhenDaily_ShouldListFiveUtcRuns()
    {
        var preview = Schedule().Preview("0 2 * * *", PinnedNow);

        preview.Valid.ShouldBeTrue();
        preview.Error.ShouldBeNull();
        preview.NextRuns.ShouldBe([.. Enumerable.Range(25, 5).Select(day => new DateTimeOffset(2026, 10, day, 2, 0, 0, TimeSpan.Zero))]);
        preview.NextRuns.ShouldAllBe(run => run.Offset == TimeSpan.Zero);
    }

    [TestCase("0 2 * *", CronSchedule.WrongFieldsError)]
    [TestCase("0 2 * * * *", CronSchedule.WrongFieldsError)]
    [TestCase("", CronSchedule.WrongFieldsError)]
    [TestCase("@daily", CronSchedule.WrongFieldsError)]
    [TestCase("61 2 * * *", CronSchedule.InvalidError)]
    [TestCase("0 2 * * mon-", CronSchedule.InvalidError)]
    [TestCase("0 0,30 1 * *", CronSchedule.InvalidError)]
    public void Preview_WhenInvalid_ShouldReturnError(string cron, string error)
    {
        var preview = Schedule().Preview(cron, PinnedNow);

        preview.ShouldBe(new CronPreview(false, error, null, []), new CronPreviewComparer());
    }

    [Test]
    public void Validate_WhenEveryMinute_ShouldRefuse()
    {
        Schedule().Validate("* * * * *").ShouldBe(CronSchedule.TooOftenError);
    }

    // On the 1st of each month only, 30 minutes apart. From 00:15 on the 1st, the first gap (00:30 to Dec 1 00:00) is a
    // month long and the 30-minute gap comes second, so a check of the first gap alone would allow it. (The brief's
    // example, "0 0,30 1 * *", puts the 30 in the hour field, so it doesn't parse; this is the schedule it means.)
    [Test]
    public void Validate_WhenGapShortOnlyLater_ShouldRefuse()
    {
        var now = new DateTimeOffset(2026, 11, 1, 0, 15, 0, TimeSpan.Zero);
        var schedule = new CronSchedule(new FakeTimeProvider(now));

        schedule.Preview("0,30 0 1 * *", now, 3).NextRuns.ShouldBe(
        [
            new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 1, 0, 30, 0, TimeSpan.Zero),
        ]);
        schedule.Validate("0,30 0 1 * *").ShouldBe(CronSchedule.TooOftenError);
    }

    // Once a year, 30 minutes apart, still inside the 400-day horizon from late October.
    [Test]
    public void Validate_WhenGapShortOnceAYear_ShouldRefuse()
    {
        Schedule().Validate("0,30 3 9 9 *").ShouldBe(CronSchedule.TooOftenError);
    }

    [TestCase("0 * * * *")]
    [TestCase("0 2 * * *")]
    [TestCase("0 0,1 * * *")]
    [TestCase("15 3 * * 1-5")]
    public void Validate_WhenAnHourOrMoreApart_ShouldAllow(string cron)
    {
        Schedule().Validate(cron).ShouldBeNull();
    }

    [Test]
    public void Validate_WhenNeverRunsInHorizon_ShouldRefuse()
    {
        Schedule().Validate("0 0 30 2 *").ShouldBe(CronSchedule.NeverError);
    }

    [TestCase("0 2 * * *", "At 02:00")]
    [TestCase("*/30 * * * *", "Every 30 minutes")]
    [TestCase("30 14 * * 1", "At 14:30, only on Monday")]
    public void Preview_WhenValid_ShouldDescribeInEnglish(string cron, string description)
    {
        Schedule().Preview(cron, PinnedNow).Description.ShouldBe(description);
    }

    [Test]
    public void Next_WhenAfterGiven_ShouldReturnFollowingOccurrence()
    {
        var schedule = Schedule();

        schedule.Next("0 2 * * *", new DateTimeOffset(2026, 10, 24, 2, 0, 0, TimeSpan.Zero)).ShouldBe(new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero));
        schedule.Next("0 2 * * *", new DateTimeOffset(2026, 10, 24, 1, 59, 0, TimeSpan.Zero)).ShouldBe(new DateTimeOffset(2026, 10, 24, 2, 0, 0, TimeSpan.Zero));
        schedule.Next("0 2 * * *", new DateTimeOffset(2026, 10, 24, 11, 0, 0, TimeSpan.FromHours(9))).ShouldBe(new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.Zero));
        schedule.Next("0 2 * *", PinnedNow).ShouldBeNull();
    }

    // The pinned examples are what this schedule says at PinnedNow (labels aside: those come from DisplayDate).
    [TestCase("valid", "0 2 * * *")]
    [TestCase("invalid", "0 2 * *")]
    [TestCase("tooOften", "*/30 * * * *")]
    public void Preview_WhenPinnedExample_ShouldMatchPinnedShape(string example, string cron)
    {
        var pinned = PinnedFixture.Read("backup-cron-preview.json")[example]!;

        var preview = Schedule().Preview(cron, PinnedNow);

        preview.Valid.ShouldBe(pinned["valid"]!.GetValue<bool>());
        preview.Error.ShouldBe(pinned["error"]?.GetValue<string>());
        preview.Description.ShouldBe(pinned["description"]?.GetValue<string>());
        preview.NextRuns.ShouldBe([.. pinned["nextRuns"]!.AsArray().Select(run => DateTimeOffset.Parse(run!["at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture))]);
    }

    private sealed class CronPreviewComparer : IEqualityComparer<CronPreview>
    {
        public bool Equals(CronPreview? x, CronPreview? y)
            => x is not null && y is not null && x.Valid == y.Valid && x.Error == y.Error && x.Description == y.Description && x.NextRuns.SequenceEqual(y.NextRuns);

        public int GetHashCode(CronPreview obj) => obj.Valid.GetHashCode();
    }
}
