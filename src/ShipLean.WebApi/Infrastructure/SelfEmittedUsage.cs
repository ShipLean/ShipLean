using ShipLean.Contracts.Usage;

namespace ShipLean.WebApi.Infrastructure;

// The events of the actions ActionUsageMap marks SelfEmitted, built the way UsageActionFilter builds them: surface web (or the one the caller passes), an
// ok outcome (they are tracked only after the thing has happened), the elapsed time, and the source of the action.
public static class SelfEmittedUsage
{
    public static void TrackOk(
        IUsageEvents events,
        TimeProvider time,
        long started,
        string name,
        Guid userId,
        string source,
        Guid? projectId = null,
        IReadOnlyDictionary<string, object>? properties = null,
        UsageSurface surface = UsageSurface.Web)
        => events.Track(new UsageEvent(
            name,
            surface,
            UsageActor.ForUser(userId),
            projectId,
            projectId is null ? 0 : 1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            source,
            time.GetUtcNow(),
            properties ?? new Dictionary<string, object>()));
}
