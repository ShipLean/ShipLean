namespace ShipLean.Contracts.Views.InspectionMetrics;

// Inspection's lead-time read (SHIP-1398 K2, K3): the one read behind GET api/projects/{projectId}/inspection-metrics and the MCP
// tool inspection_metrics. The caller must be a member of the project (non-members get not_found); the query is validated after
// that: an unknown range or cut, a period that is not one of the project's sprints, or a select without a period is validation.
public interface IInspectionMetrics
{
    Task<InspectionMetricsViewDto> GetAsync(Guid projectId, InspectionMetricsQuery query, CancellationToken ct);
}
