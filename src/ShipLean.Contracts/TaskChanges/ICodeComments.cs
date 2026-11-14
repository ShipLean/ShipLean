namespace ShipLean.Contracts.TaskChanges;

// Code comments on a Task or Bug. Callers check IProjectAccess first; this trusts them. Writes publish codeCommentsChanged.
public interface ICodeComments
{
    // The repo and sha must be one of the item's live commits, otherwise Validation (field "sha").
    Task<CodeComment> AddAsync(Guid workItemId, NewCodeComment comment, CancellationToken cancellationToken = default);

    // The comment must be on that item and top level: NotFound or Validation otherwise.
    Task<CodeComment> ReplyAsync(Guid workItemId, Guid commentId, string body, CancellationToken cancellationToken = default);

    Task ResolveAsync(Guid workItemId, Guid commentId, bool resolved, CancellationToken cancellationToken = default);

    // Top-level comments with their replies, oldest first.
    Task<IReadOnlyList<CodeComment>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);

    // The top-level comments that are neither resolved nor archived.
    Task<IReadOnlyList<CodeComment>> OpenAsync(Guid workItemId, CancellationToken cancellationToken = default);

    // Replies to each comment and resolves it. An id not on the item is Validation (field "answered_comments") and nothing is
    // written. Idempotent: a comment already resolved whose last reply is from the same caller with the same text is skipped.
    Task AnswerAsync(Guid workItemId, IReadOnlyList<CodeCommentAnswer> answers, CancellationToken cancellationToken = default);

    // An open comment goes outdated when an added commit in the same repository has a hunk touching its path and line on its side.
    Task MarkOutdatedAsync(Guid workItemId, IReadOnlyList<TaskCommit> added, CancellationToken cancellationToken = default);
}
