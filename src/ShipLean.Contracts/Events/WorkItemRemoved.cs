namespace ShipLean.Contracts.Events;

public sealed record WorkItemRemoved(Guid ProjectId, Guid Id, Guid? ParentId) : ProjectEvent(ProjectId);
