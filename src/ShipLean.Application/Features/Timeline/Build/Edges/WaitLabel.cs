using System.Globalization;

namespace ShipLean.Application.Features.Timeline.Build.Edges;

// The label of a wait (D6): "<N> h" from 12 h up to a day, "<N> days" from a day on, one decimal with a trailing .0 dropped;
// null under 12 h (an overlap is negative, so it never gets one).
internal static class WaitLabel
{
    private const double MinimumHours = 12;
    private const double HoursPerDay = 24;

    public static double RoundOne(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    public static string? For(double waitHours)
    {
        if (waitHours < MinimumHours)
        {
            return null;
        }

        return waitHours < HoursPerDay
            ? $"{Format(waitHours)} h"
            : $"{Format(waitHours / HoursPerDay)} days";
    }

    private static string Format(double value) => RoundOne(value).ToString("0.#", CultureInfo.InvariantCulture);
}
