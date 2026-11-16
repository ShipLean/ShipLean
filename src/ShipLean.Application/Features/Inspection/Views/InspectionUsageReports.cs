using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Settings;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Inspection.Views;

// The SPA's report of what a viewer did on Agent runs (D22): the caller must be a member, the project must have Inspection on, and then one event is tracked.
[RegisterPerRequest]
public sealed class InspectionUsageReports(InspectionGate gate, InspectionProjectStore store, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
{
    public async Task ReportAsync(Guid projectId, InspectionUsageDto report, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        // Access first: the store's member check answers not_found for a non-member before anything else is looked at.
        var key = await store.KeyOfAsync(projectId, ct);
        await gate.RequireRunsAsync(key, ct);
        events.Track(new UsageEvent(
            report.Action switch
            {
                InspectionUsageAction.SectionSeen => UsageEventNames.InspectionSectionSeen,
                InspectionUsageAction.RunOpened => UsageEventNames.InspectionRunOpened,
                InspectionUsageAction.RowSelected => UsageEventNames.InspectionRowSelected,
                InspectionUsageAction.Zoomed => UsageEventNames.InspectionZoomed,
                InspectionUsageAction.ErrorsOnly => UsageEventNames.InspectionErrorsOnly,
                _ => throw ApiException.Validation("That is not an Inspection action.", "action"),
            },
            UsageSurface.Web,
            UsageActor.ForUser(currentUser.Id),
            projectId,
            1,
            UsageOutcome.Ok,
            null,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            "api:InspectionUsage.Report",
            time.GetUtcNow(),
            new Dictionary<string, object>()));
    }
}
