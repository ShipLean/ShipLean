namespace ShipLean.Contracts.Backlog;

// Id is one of BacklogCheckIds.All; ItemIds are the rows of the top of the stack that fail it.
public sealed record BacklogReviewCheckDto(string Id, IReadOnlyList<Guid> ItemIds);
