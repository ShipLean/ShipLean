using ShipLean.Contracts.WorkItems;

namespace ShipLean.TestSupport.Contracts.WorkItems;

public sealed record RuleCall(WorkItemSnapshot? Before, ProposedChange Change, Guid? ActorId);
