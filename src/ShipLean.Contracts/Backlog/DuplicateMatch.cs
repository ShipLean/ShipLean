namespace ShipLean.Contracts.Backlog;

// Index is the title's place in the request.
public sealed record DuplicateMatch(int Index, IReadOnlyList<DuplicateItem> Items);
