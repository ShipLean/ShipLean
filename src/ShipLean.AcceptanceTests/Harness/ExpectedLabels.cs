using System.Globalization;

namespace ShipLean.AcceptanceTests.Harness;

// Expected labels, computed here from the documented formats (DisplayDate's comments) rather than by calling DisplayDate,
// so a wrong DisplayDate cannot also make the expectation wrong. Time is never frozen. Use for timestamps the test
// itself created in the run, e.g. "just now" after an edit. The overloads without a zone are for viewers with no zone
// (UTC); a BangkokViewerTest passes ViewerZoneTestData.Bangkok.
public static class ExpectedLabels
{
    public static string Relative(DateTimeOffset value) => Relative(value, TimeZoneInfo.Utc);

    // "just now", "5m ago", "3h ago" (same day in the zone), "Yesterday", else the short day.
    public static string Relative(DateTimeOffset value, TimeZoneInfo zone)
    {
        var now = TimeProvider.System.GetUtcNow();
        var elapsed = now - value;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        return DaysFromToday(value, now, zone) switch
        {
            0 => $"{(int)elapsed.TotalHours}h ago",
            -1 => "Yesterday",
            _ => ShortDay(value, now, zone),
        };
    }

    // "today at 07:02", "yesterday at 07:02", "tomorrow at 07:02", else "2 Mar at 07:02" or "2 Mar 2025 at 07:02".
    public static string DayAt(DateTimeOffset value, TimeZoneInfo zone)
    {
        var now = TimeProvider.System.GetUtcNow();
        var day = DaysFromToday(value, now, zone) switch
        {
            0 => "today",
            -1 => "yesterday",
            1 => "tomorrow",
            _ => ShortDay(value, now, zone),
        };
        return $"{day} at {Time(value, zone)}";
    }

    // The 24-hour time of day in the zone: "07:02".
    public static string Time(DateTimeOffset value, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(value, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string JustNow() => Relative(TimeProvider.System.GetUtcNow());

    // "2 Mar" in the current year (in the zone), else "2 Mar 2025".
    private static string ShortDay(DateTimeOffset value, DateTimeOffset now, TimeZoneInfo zone)
    {
        var day = TimeZoneInfo.ConvertTime(value, zone);
        var format = day.Year == TimeZoneInfo.ConvertTime(now, zone).Year ? "d MMM" : "d MMM yyyy";
        return day.ToString(format, CultureInfo.InvariantCulture);
    }

    private static int DaysFromToday(DateTimeOffset value, DateTimeOffset now, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime).DayNumber
        - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).DayNumber;
}
