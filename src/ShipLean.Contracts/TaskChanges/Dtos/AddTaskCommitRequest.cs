namespace ShipLean.Contracts.TaskChanges.Dtos;

// POST api/work-items/{id}/commits. Sha may be a commit URL when Repo is empty.
public sealed record AddTaskCommitRequest(string? Repo, string Sha);
