using System.Globalization;

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
}
