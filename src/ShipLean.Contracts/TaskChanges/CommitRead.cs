namespace ShipLean.Contracts.TaskChanges;

public sealed record CommitRead(CommitReadStatus Status, CommitMeta? Meta, int? HttpStatus);
