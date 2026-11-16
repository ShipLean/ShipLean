namespace ShipLean.Contracts.Inspection;

// POST api/projects/{projectId}/usage/inspection.
public sealed record InspectionUsageDto(InspectionUsageAction Action, string ItemKey);
