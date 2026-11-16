namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/inspection-metrics-viewed: the SPA reports an Inspection view shown (K11). View is overview,
// leadtime, period, quality or json.
public sealed record InspectionMetricsViewedDto(string View);
