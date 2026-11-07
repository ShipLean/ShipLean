using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's report that a screen was opened: a project must be one the caller is a member of (which also stamps the usage scope),
// then the screen must be a known route id, and then screen.viewed is tracked, with the request's viewport when it sent one.
// Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class ScreenViews(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, IUsageScope scope, TimeProvider time)
{
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
        var properties = new Dictionary<string, object> { [UsageProps.Screen] = view.Screen };
        if (scope.Viewport is { } viewport)
        {
            properties[UsageProps.Viewport] = viewport;
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
