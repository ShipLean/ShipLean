using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's reports from a board's filter: the Filter control opened (the sheet on a phone, the bar elsewhere) and the Health
// filter changed. The caller must be a member of the project (which also stamps the usage scope); then the event is tracked.
// Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class BoardFilterUsage(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    private static readonly HashSet<string> Healths = new(StringComparer.Ordinal) { "onTrack", "atRisk", "blocked", "none", "any" };

    private static readonly HashSet<string> Levels = new(StringComparer.Ordinal) { "features", "stories", "epics" };

    public async Task OpenedAsync(Guid projectId, BoardFilterOpenedDto opened, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        if (!Levels.Contains(opened.Level))
        {
            throw ApiException.Validation("Level must be features, stories or epics.", "level");
        }
        Track(UsageEventNames.BoardFilterOpened, projectId, started, "api:ProjectUsage.BoardFilterOpened",
            new Dictionary<string, object> { [UsageProps.Level] = opened.Level });
    }

    public async Task HealthFilteredAsync(Guid projectId, BoardHealthFilteredDto filtered, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        if (!Healths.Contains(filtered.Health))
        {
            throw ApiException.Validation("Health must be onTrack, atRisk, blocked, none or any.", "health");
        }
        Track(UsageEventNames.BoardHealthFiltered, projectId, started, "api:ProjectUsage.BoardHealthFiltered",
            new Dictionary<string, object> { [UsageProps.Health] = filtered.Health });
    }

    private void Track(string name, Guid projectId, long started, string source, Dictionary<string, object> properties)
        => events.Track(new UsageEvent(
            name,
            UsageSurface.Web,
            UsageActor.ForUser(currentUser.Id),
            projectId,
            1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            source,
            time.GetUtcNow(),
            properties));
}
