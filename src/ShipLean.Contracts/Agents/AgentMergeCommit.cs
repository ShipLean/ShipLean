namespace ShipLean.Contracts.Agents;

// A merge commit of a related item's pull request: the SHA (lowercase hex) and when it merged, if known.
public sealed record AgentMergeCommit(string Sha, DateTimeOffset? MergedAt);
