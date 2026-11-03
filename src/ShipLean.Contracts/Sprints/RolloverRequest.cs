namespace ShipLean.Contracts.Sprints;

// EndToday closes a sprint that is still running: its end date moves to today in the same transaction as the moves.
public sealed record RolloverRequest(IReadOnlyList<RolloverMove> Moves, bool EndToday = false);
