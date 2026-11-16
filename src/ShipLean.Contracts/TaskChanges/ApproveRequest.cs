namespace ShipLean.Contracts.TaskChanges;

// HeadCommitId is the work_item_commit id of the newest commit the reviewer saw (D21).
public sealed record ApproveRequest(string? Note, Guid HeadCommitId);
