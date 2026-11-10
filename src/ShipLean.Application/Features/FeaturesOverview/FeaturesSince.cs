using ShipLean.Core;

namespace ShipLean.Application.Features.FeaturesOverview;

// The moment the Features overview and digest count changes from, and its label in the clock's zone. Never seen: 24 hours back.
// "since yesterday" when it falls on the day before today, "since 09:12" today (or later, clock skew), otherwise "since Mon 09:12".
public static class FeaturesSince
{
    public static readonly TimeSpan NeverSeenWindow = TimeSpan.FromHours(24);

    public static (DateTimeOffset Since, string SinceLabel) Of(DateTimeOffset? seenAt, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        var since = seenAt ?? now - NeverSeenWindow;
        var label = DisplayDate.DaysUntil(since, now, clock.LocalTimeZone) switch
        {
            -1 => "since yesterday",
            >= 0 => $"since {DisplayDate.Clock(since, clock)}",
            _ => $"since {DisplayDate.Weekday(since, clock)} {DisplayDate.Clock(since, clock)}",
        };
        return (since, label);
    }
}
