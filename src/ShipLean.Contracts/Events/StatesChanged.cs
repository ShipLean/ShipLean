namespace ShipLean.Contracts.Events;

public sealed record StatesChanged(Guid ProjectId) : ProjectEvent(ProjectId);
