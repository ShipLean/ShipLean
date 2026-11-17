using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// The SPA's reports from Inspection (SHIP-1398 K11): a view shown, a sprint opened and a task opened. The caller must be a member of
// the project (which also stamps the usage scope); then the value is checked against its closed set and the event is tracked.
// Nothing here waits on delivery.
[RegisterPerRequest]
public sealed class InspectionMetricsUsage(IProjectAccess access, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    private static readonly HashSet<string> Views = new(StringComparer.Ordinal) { "overview", "leadtime", "period", "quality", "cost", "json" };
    private static readonly HashSet<string> Periods = new(StringComparer.Ordinal) { "finished", "running" };
    private static readonly HashSet<string> Froms = new(StringComparer.Ordinal) { "rail", "longest", "chart", "mostTokens" };

    public Task ViewedAsync(Guid projectId, InspectionMetricsViewedDto viewed, CancellationToken cancellationToken)
        => ReportAsync(projectId, UsageEventNames.InspectionMetricsViewed, "api:ProjectUsage.InspectionMetricsViewed",
            UsageProps.View, viewed.View, Views, "View must be overview, leadtime, period, quality, cost or json.", cancellationToken);

    public Task PeriodOpenedAsync(Guid projectId, InspectionMetricsPeriodOpenedDto opened, CancellationToken cancellationToken)
        => ReportAsync(projectId, UsageEventNames.InspectionMetricsPeriodOpened, "api:ProjectUsage.InspectionMetricsPeriodOpened",
            UsageProps.Period, opened.Period, Periods, "Period must be finished or running.", cancellationToken);

    public Task TaskOpenedAsync(Guid projectId, InspectionMetricsTaskOpenedDto opened, CancellationToken cancellationToken)
        => ReportAsync(projectId, UsageEventNames.InspectionMetricsTaskOpened, "api:ProjectUsage.InspectionMetricsTaskOpened",
            UsageProps.From, opened.From, Froms, "From must be rail, longest, chart or mostTokens.", cancellationToken);

    private async Task ReportAsync(
        Guid projectId, string name, string source, string prop, string value, HashSet<string> allowed, string message, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        if (!allowed.Contains(value))
        {
            throw ApiException.Validation(message, prop);
        }
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
            new Dictionary<string, object> { [prop] = value }));
    }
}
