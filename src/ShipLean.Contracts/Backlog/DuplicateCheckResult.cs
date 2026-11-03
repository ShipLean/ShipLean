namespace ShipLean.Contracts.Backlog;

// Only titles with a match.
public sealed record DuplicateCheckResult(IReadOnlyList<DuplicateMatch> Matches);
