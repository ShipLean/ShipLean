namespace ShipLean.Contracts.Inspection;

// Store unreachable: Reachable is false and Problem says why; the port never throws for it.
public sealed record RunSearch(IReadOnlyList<RunSummary> Runs, bool Reachable, string? Problem);
