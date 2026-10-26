namespace ShipLean.Contracts.Sprints;

// TargetSprintId null = back to the backlog.
public sealed record RolloverMove(Guid WorkItemId, Guid? TargetSprintId);
