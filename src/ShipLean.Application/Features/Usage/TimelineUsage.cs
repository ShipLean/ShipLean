using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's reports from the Feature timeline (D14, D20): the page opened, from the Feature page or a link, and a bar selected.
// The caller must be a member of the project (which also stamps the usage scope); then the event is tracked. Nothing here waits
// on delivery.
[RegisterPerRequest]
public sealed class TimelineUsage(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    private static readonly HashSet<string> Froms = new(StringComparer.Ordinal) { "feature", "link" };

    public async Task OpenedAsync(Guid projectId, TimelineOpenedDto opened, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        if (!Froms.Contains(opened.From))
        {
            throw ApiException.Validation("From must be feature or link.", "from");
        }
        Track(UsageEventNames.TimelineOpened, projectId, started, "api:ProjectUsage.TimelineOpened",
            new Dictionary<string, object> { [UsageProps.From] = opened.From });
    }

    public async Task BarSelectedAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        Track(UsageEventNames.TimelineBarSelected, projectId, started, "api:ProjectUsage.TimelineBarSelected", new Dictionary<string, object>());
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
