namespace ShipLean.Contracts.Events;

public sealed record WorkItemReordered(Guid ProjectId, Guid Id, string BacklogOrder) : ProjectEvent(ProjectId);
