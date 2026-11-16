namespace ShipLean.Contracts.TaskChanges.Dtos;

// HeadCommitId is the id of the newest commit the reviewer saw; a newer one makes Approve answer conflict.
public sealed record ApproveChangesRequest(string? Note, Guid HeadCommitId);
