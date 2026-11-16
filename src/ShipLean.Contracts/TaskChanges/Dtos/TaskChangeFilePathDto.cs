namespace ShipLean.Contracts.TaskChanges.Dtos;

// One distinct file a Task's commits touch, so the client can count "n of m files viewed" without fetching patches.
public sealed record TaskChangeFilePathDto(string Repo, string Path);
