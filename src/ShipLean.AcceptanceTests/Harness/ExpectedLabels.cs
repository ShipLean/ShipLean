using ShipLean.Core;

namespace ShipLean.AcceptanceTests.Harness;

// Expected labels, computed the way the server computes them (time is never frozen). Use for timestamps the test itself
// created in the run, e.g. "just now" after an edit. The overloads without a zone are for viewers with no zone (UTC);
// a BangkokViewerTest passes ViewerZoneTestData.Bangkok.
public static class ExpectedLabels
{
    public static string Relative(DateTimeOffset value) => Relative(value, TimeZoneInfo.Utc);

    public static string Relative(DateTimeOffset value, TimeZoneInfo zone) => DisplayDate.Relative(value, TimeProvider.System.GetUtcNow(), zone);

    public static string DayAt(DateTimeOffset value, TimeZoneInfo zone) => DisplayDate.DayAt(value, TimeProvider.System.GetUtcNow(), zone);

    public static string Time(DateTimeOffset value, TimeZoneInfo zone) => DisplayDate.Time(value, zone);

    public static string JustNow() => Relative(TimeProvider.System.GetUtcNow());
}
