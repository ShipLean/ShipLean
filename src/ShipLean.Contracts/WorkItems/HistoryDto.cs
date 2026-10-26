namespace ShipLean.Contracts.WorkItems;

// Newest first.
public sealed record HistoryDto(IReadOnlyList<RevisionDto> Revisions);
