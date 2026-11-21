using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's reports from Project settings > Prompts (SHIP-744 C9): the tab opened, and Customize opened on one prompt. The caller must be a
// member of the project (which also stamps the usage scope); then the event is tracked. Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class PromptsUsage(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    public Task OpenedAsync(Guid projectId, CancellationToken cancellationToken)
        => ReportAsync(projectId, UsageEventNames.PromptsOpened, "api:ProjectUsage.PromptsOpened", null, cancellationToken);

    public Task CustomizeOpenedAsync(Guid projectId, PromptCustomizeOpenedDto opened, CancellationToken cancellationToken)
        => ReportAsync(projectId, UsageEventNames.PromptCustomizeOpened, "api:ProjectUsage.PromptCustomizeOpened",
            new Dictionary<string, object> { [UsageProps.Prompt] = PromptKeys.ToWire(opened.Prompt) }, cancellationToken);

    private async Task ReportAsync(Guid projectId, string name, string source, Dictionary<string, object>? properties, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        events.Track(new UsageEvent(
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
            properties ?? []));
    }
}
