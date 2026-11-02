namespace ShipLean.Contracts.Backlog;

// 1 to BulkLimits.MaxRows titles.
public sealed record DuplicateCheckRequest(IReadOnlyList<string> Titles);
