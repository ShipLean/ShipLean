namespace ShipLean.Contracts.Projects;

// Items in StateId move to TargetStateId (same type) when the state goes.
public sealed record StateRemove(Guid StateId, Guid TargetStateId);
