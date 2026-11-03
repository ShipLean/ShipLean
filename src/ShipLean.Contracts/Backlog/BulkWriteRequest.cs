namespace ShipLean.Contracts.Backlog;

public sealed record BulkWriteRequest(IReadOnlyList<BulkWriteItem> Items);
