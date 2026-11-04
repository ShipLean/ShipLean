namespace ShipLean.Contracts.Events;

// A sprint was closed (the rollover was decided, from Close sprint or the MCP end_sprint) by ClosedBy. Published after SprintChanged.
public sealed record SprintClosed(Guid ProjectId, Guid SprintId, Guid ClosedBy) : ProjectEvent(ProjectId);
