using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's report that a screen was opened: a project must be one the caller is a member of (which also stamps the usage scope),
// then the screen must be a known route id and a mode, when given, a backlog mode, and then screen.viewed is tracked (with the mode). Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class ScreenViews(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal) { "list", "planning", "tree" };

    public async Task ReportAsync(ScreenViewDto view, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        if (view.ProjectId is { } projectId)
        {
            await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        }
        if (!ScreenIds.All.Contains(view.Screen))
        {
            throw ApiException.Validation("That is not a known screen.", "screen");
        }
        if (view.Mode is not null && !Modes.Contains(view.Mode))
        {
            throw ApiException.Validation("That is not a backlog mode.", "mode");
        }
        var properties = new Dictionary<string, object> { [UsageProps.Screen] = view.Screen };
        if (view.Mode is not null)
        {
            properties[UsageProps.Mode] = view.Mode;
        }
        events.Track(new UsageEvent(
            UsageEventNames.ScreenViewed,
            UsageSurface.Web,
            UsageActor.ForUser(currentUser.Id),
            view.ProjectId,
            view.ProjectId is null ? 0 : 1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            "screen",
            time.GetUtcNow(),
            properties));
    }
}
