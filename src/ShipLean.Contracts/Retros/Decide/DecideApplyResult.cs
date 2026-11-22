namespace ShipLean.Contracts.Retros.Decide;

// An apply with the result of each action of the plan, one per plan action in the plan's order.
public sealed record DecideApplyResult(DecideOutcome Outcome, IReadOnlyList<DecideActionResult> Actions);
