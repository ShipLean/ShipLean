namespace ShipLean.Contracts.Search;

// One thing to index: a comment, an item, … by its own id.
public sealed record SearchSource(SearchSourceKind Kind, Guid Id);
