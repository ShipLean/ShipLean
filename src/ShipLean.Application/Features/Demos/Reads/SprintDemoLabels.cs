using System.Globalization;
using ShipLean.Core;

namespace ShipLean.Application.Features.Demos.Reads;

// The labels the Demo tab shows, made here so the client formats nothing.
internal static class SprintDemoLabels
{
    // "1:05", or "1:02:03" from one hour. Whole seconds, rounded down, as a player shows them.
    public static string Duration(decimal seconds)
    {
        var total = (long)Math.Floor(Math.Max(0m, seconds));
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var rest = total % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{rest:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{rest:00}");
    }

    // "today at 10:15", "yesterday at 10:15", "tomorrow at 10:15", else "2 Mar at 10:15", in the clock's time zone.
    public static string When(DateTimeOffset value, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var zone = clock.LocalTimeZone;
        var day = DisplayDate.DaysUntil(value, now, zone) switch
        {
            0 => "today",
            -1 => "yesterday",
            1 => "tomorrow",
            _ => DisplayDate.ShortDay(value, now, zone),
        };
        return $"{day} at {DisplayDate.Clock(value, clock)}";
    }
}
