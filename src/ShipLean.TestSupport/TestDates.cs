namespace ShipLean.TestSupport;

// Dates for test data that keep working as the calendar moves on. A fixed date in data the app later compares with a real
// clock (a cookie jar, Postgres now(), a seeded row) fails once the calendar passes it; AG0051 reports those literals.
public static class TestDates
{
    // Midnight UTC today by the real clock. Whole seconds, so it round-trips through Postgres and JSON unchanged.
    public static DateTimeOffset Today => new(TimeProvider.System.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);

    // Later than anything a test or the app writes, without being a year the calendar will reach.
    public static DateTimeOffset FarFuture => Today.AddYears(50);
}
