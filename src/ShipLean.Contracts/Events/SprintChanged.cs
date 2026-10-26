namespace ShipLean.Contracts.Events;

public sealed record SprintChanged(Guid ProjectId, Guid SprintId) : ProjectEvent(ProjectId);
