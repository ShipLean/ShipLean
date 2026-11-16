using System.Globalization;
using ShipLean.Core;

namespace ShipLean.Application.Features.Sprints.Metrics;

// The labels of a burndown day. A sprint day is a calendar day shared by every viewer in the project, so the labels are of the UTC day
// and do not follow the viewer's zone; the client prints them as given.
internal static class BurndownLabels
{
    // "2 Nov" in the year of today, "2 Nov 2025" otherwise.
    public static string Label(DateOnly day, DateOnly today) =>
        DisplayDate.ShortDay(AtMidnightUtc(day), AtMidnightUtc(today), TimeZoneInfo.Utc);

    // The day of the month, "2", for the chart's axis.
    public static string Day(DateOnly day) => day.Day.ToString(CultureInfo.InvariantCulture);

    private static DateTimeOffset AtMidnightUtc(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
