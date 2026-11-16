namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/inspection-metrics-task-opened: the SPA reports a task opened on Inspection (K11). From is where:
// rail (the one-sprint rail), longest (the Longest tasks list) or chart (a dot on the lead-time chart).
public sealed record InspectionMetricsTaskOpenedDto(string From);
