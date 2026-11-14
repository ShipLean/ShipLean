namespace ShipLean.Contracts.TaskChanges;

// Approve and Request changes on a Task or Bug. Callers check IProjectAccess first; this trusts them.
public interface ICodeReview
{
    // Conflict when HeadCommitId isn't the item's newest live work_item_commit (D21). Otherwise the item moves to its first
    // Completed state with a review revision ("Approved a81d4e0, 3c9f2b1"). Open comments stay open; the note becomes a comment.
    Task<ReviewOutcome> ApproveAsync(Guid workItemId, ApproveRequest request, CancellationToken cancellationToken = default);

    // The item moves to its first Proposed state and is unassigned; with KeepAssignee it moves to its first InProgress state
    // and keeps its assignee. Open top-level comments get sent_back_at. A review revision ("Changes requested · n comments");
    // the note becomes a comment.
    Task<ReviewOutcome> RequestChangesAsync(Guid workItemId, RequestChangesRequest request, CancellationToken cancellationToken = default);
}
