using ShipLean.Contracts.Asks;

namespace ShipLean.Contracts.WorkItems;

// Newest first: the field changes, and separately the decision and question lines.
public sealed record HistoryDto(IReadOnlyList<RevisionDto> Revisions, IReadOnlyList<AskHistoryLineDto> Asks);
