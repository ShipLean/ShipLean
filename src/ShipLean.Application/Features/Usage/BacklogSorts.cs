using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's report of a click on a backlog column title: the caller must be a member of the project (which also stamps the usage
// scope), then the screen, column and direction must be known ones, and then backlog.sorted is tracked. Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class BacklogSorts(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    private static readonly HashSet<string> Screens = new(StringComparer.Ordinal) { "storiesBacklog", "featuresBacklog", "epicsBacklog" };

    // The list columns of every backlog level (src/ShipLean.Client/src/features/backlog/sort/columnSort.ts sorts by these).
    private static readonly HashSet<string> Columns = new(StringComparer.Ordinal)
    {
        "rank", "key", "title", "parent", "state", "points", "sprint", "assignee", "tags", "features", "progress",
    };

    private static readonly HashSet<string> Directions = new(StringComparer.Ordinal) { "asc", "desc", "off" };

    public async Task ReportAsync(Guid projectId, BacklogSortDto sort, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        if (!Screens.Contains(sort.Screen))
        {
            throw ApiException.Validation("That is not a backlog screen.", "screen");
        }
        if (!Columns.Contains(sort.Column))
        {
            throw ApiException.Validation("That is not a backlog column.", "column");
        }
        if (!Directions.Contains(sort.Direction))
        {
            throw ApiException.Validation("The direction must be asc, desc or off.", "direction");
        }
        events.Track(new UsageEvent(
            UsageEventNames.BacklogSorted,
            UsageSurface.Web,
            UsageActor.ForUser(currentUser.Id),
            projectId,
            1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            "api:ProjectUsage.BacklogSort",
            time.GetUtcNow(),
            new Dictionary<string, object>
            {
                [UsageProps.Screen] = sort.Screen,
                [UsageProps.Column] = sort.Column,
                [UsageProps.Direction] = sort.Direction,
            }));
    }
}
