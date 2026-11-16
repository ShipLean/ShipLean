namespace ShipLean.Contracts.TaskChanges.Dtos;

// Files is the distinct (repo, path) count across the commits' paths.
public sealed record TaskCommitTotalsDto(int Commits, int Files, int Additions, int Deletions);
