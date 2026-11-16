using ShipLean.Core;

namespace ShipLean.TestSupport.Identity;

// Zones and instants for viewer time-zone tests. Expected labels are literal strings or worked out by hand from the
// instant (ToOffset), never through DisplayDate, so a wrong DisplayDate cannot also make the expectation wrong.
public static class ViewerZoneTestData
{
    public const string BangkokId = "Asia/Bangkok";

    public static TimeZoneInfo Bangkok { get; } =
        DisplayDate.TryZone(BangkokId, out var zone) ? zone : throw new InvalidOperationException("This host has no Asia/Bangkok zone.");

    // 20:00 UTC on a day two years before the run's clock: Bangkok (UTC+07:00) reads 03:00 the next day, UTC the same day,
    // and as an earlier year every short day label carries its year.
    public static DateTimeOffset LateEveningUtc => TestDates.Today.AddYears(-2).AddHours(20);
}
