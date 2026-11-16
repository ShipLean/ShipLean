namespace ShipLean.Contracts.TaskChanges;

public sealed record AddCommitsOutcome(IReadOnlyList<TaskCommit> Added, IReadOnlyList<TaskCommit> Skipped);
