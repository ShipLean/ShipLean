using System.Globalization;

namespace ShipLean.Core;

// How a sprint's dates become its phase and labels. The phase comes from the dates and today (the SPA has no clock).
public static class SprintLabels
{
    // Sprint dates are calendar days in UTC (the seed's dates are), so "today" is the UTC day, not the host's local one.
    public static DateOnly Today(TimeProvider clock) => DisplayDate.Today(clock.GetUtcNow(), TimeZoneInfo.Utc);

    public static SprintPhase Phase(DateOnly start, DateOnly end, DateOnly today)
    {
        if (today < start)
        {
            return SprintPhase.Future;
        }
        return today > end ? SprintPhase.Past : SprintPhase.Current;
    }

    // "20 Oct – 2 Nov"; the year is added to both ends when either is not this year.
    public static string Dates(DateOnly start, DateOnly end, DateOnly today)
    {
        var format = start.Year == today.Year && end.Year == today.Year ? "d MMM" : "d MMM yyyy";
        return $"{start.ToString(format, CultureInfo.InvariantCulture)} – {end.ToString(format, CultureInfo.InvariantCulture)}";
    }

    // "9 days left" | "Ends today" | "Ended 3 days ago" | "Starts in 5 days".
    public static string Time(DateOnly start, DateOnly end, DateOnly today) => Phase(start, end, today) switch
    {
        SprintPhase.Future => $"Starts in {Days(start.DayNumber - today.DayNumber)}",
        SprintPhase.Past => $"Ended {Days(today.DayNumber - end.DayNumber)} ago",
        _ => end == today ? "Ends today" : $"{Days(end.DayNumber - today.DayNumber)} left",
    };

    private static string Days(int count) => count == 1 ? "1 day" : $"{count} days";
}
