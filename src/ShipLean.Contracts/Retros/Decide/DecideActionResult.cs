namespace ShipLean.Contracts.Retros.Decide;

// What an apply did with one action of the plan, in plan order. Key is the action item it created or added the points to; null when it was skipped,
// with the reason in Skipped. Created is true for a new action item.
public sealed record DecideActionResult(string? Key, bool Created, string? Skipped);
