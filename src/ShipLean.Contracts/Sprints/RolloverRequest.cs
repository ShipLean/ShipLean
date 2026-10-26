namespace ShipLean.Contracts.Sprints;

public sealed record RolloverRequest(IReadOnlyList<RolloverMove> Moves);
