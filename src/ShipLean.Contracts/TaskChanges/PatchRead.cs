namespace ShipLean.Contracts.TaskChanges;

public sealed record PatchRead(CommitReadStatus Status, string? UnifiedDiff);
