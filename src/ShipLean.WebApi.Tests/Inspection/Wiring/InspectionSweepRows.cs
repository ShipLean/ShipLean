using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// The Inspection routes. The sweeps' hosts have Inspection on and SHIP enabled (InspectionSweepHost), so the allowed leg gets in.
public sealed class InspectionSweepRows : IAccessSweepRows
{
    private const string Runs = "api/inspection/projects/SHIP/items/" + InspectionSweepHost.Item + "/runs";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/inspection/projects/{project}/items/{key}/runs", ProjectRole.Member, Url: Runs),
        new("GET", "api/inspection/projects/{project}/items/{key}/runs/{runId}", ProjectRole.Member, Url: $"{Runs}/{InspectionSweepHost.Run.Summary.RunId}"),
        new("GET", "api/inspection/projects/{project}/items/{key}/runs/{runId}/rows/{rowId}", ProjectRole.Member,
            Url: $"{Runs}/{InspectionSweepHost.Run.Summary.RunId}/rows/{InspectionSweepHost.Run.Spans[0].SpanId}"),
        new("GET", "api/inspection/projects/{project}/settings", ProjectRole.Member, Url: "api/inspection/projects/SHIP/settings"),
        new("PUT", "api/inspection/projects/{project}/settings", ProjectRole.Admin, Url: "api/inspection/projects/SHIP/settings",
            Body: """{"enabled":true,"showContent":false,"tenantOverride":null,"selectorOverride":null,"storeMode":"tempoLoki"}"""),
        new("GET", "api/inspection/projects/{project}/agent-setup", ProjectRole.Member,
            Url: "api/inspection/projects/SHIP/agent-setup?agent=claude-code&item=" + InspectionSweepHost.Item),
        new("POST", "api/projects/{projectId:guid}/usage/inspection", ProjectRole.Member, Body: """{"action":"sectionSeen","itemKey":"SHIP-124"}"""),
        new("GET", "api/inspection/health", ProjectRole.Member, InstanceScoped: true),
    ];
}
