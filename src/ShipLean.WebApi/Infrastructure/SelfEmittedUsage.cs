using ShipLean.Contracts.Usage;

namespace ShipLean.WebApi.Infrastructure;

// The events of the actions ActionUsageMap marks SelfEmitted, built the way UsageActionFilter builds them: surface web, an
// ok outcome (they are tracked only after the thing has happened), the elapsed time, the source of the action, and the
// request's viewport (from the scope) merged with the caller's properties.
public static class SelfEmittedUsage
{
    public static void TrackOk(
        IUsageEvents events,
        IUsageScope scope,
        TimeProvider time,
        long started,
        string name,
        Guid userId,
        string source,
        Guid? projectId = null,
        IReadOnlyDictionary<string, object>? properties = null)
    {
        var merged = properties is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(properties, StringComparer.Ordinal);
        if (scope.Viewport is { } viewport)
        {
            merged[UsageProps.Viewport] = viewport;
        }
        events.Track(new UsageEvent(
            name,
            UsageSurface.Web,
            UsageActor.ForUser(userId),
            projectId,
            projectId is null ? 0 : 1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            source,
            time.GetUtcNow(),
            merged));
    }
}
