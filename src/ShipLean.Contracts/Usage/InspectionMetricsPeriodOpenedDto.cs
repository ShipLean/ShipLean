namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/inspection-metrics-period-opened: the SPA reports a sprint opened on Inspection (K11). Period is
// the kind of sprint, finished or running, not its id.
public sealed record InspectionMetricsPeriodOpenedDto(string Period);
