using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's report from the retro insights Themes tab: a theme's sheet or the Unthemed sheet opened. The caller must be a member of
// the project (which also stamps the usage scope); then the value is checked against its closed set and the event is tracked.
// Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class RetroThemeSheetUsage(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    private static readonly HashSet<string> Sheets = new(StringComparer.Ordinal) { "theme", "unthemed" };

    public async Task OpenedAsync(Guid projectId, RetroThemeSheetOpenedDto opened, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        if (!Sheets.Contains(opened.Sheet))
        {
            throw ApiException.Validation("Sheet must be theme or unthemed.", "sheet");
        }
        events.Track(new UsageEvent(
            UsageEventNames.RetroThemeSheetOpened,
            UsageSurface.Web,
            UsageActor.ForUser(currentUser.Id),
            projectId,
            1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            "api:ProjectUsage.RetroThemeSheetOpened",
            time.GetUtcNow(),
            new Dictionary<string, object> { [UsageProps.Sheet] = opened.Sheet }));
    }
}
