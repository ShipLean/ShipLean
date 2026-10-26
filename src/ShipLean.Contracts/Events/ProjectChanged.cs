namespace ShipLean.Contracts.Events;

public sealed record ProjectChanged(Guid ProjectId) : ProjectEvent(ProjectId);
