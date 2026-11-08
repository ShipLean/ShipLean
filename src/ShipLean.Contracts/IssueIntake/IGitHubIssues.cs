namespace ShipLean.Contracts.IssueIntake;

// Writes to (and the one read back from) GitHub issues, with the instance token. Only the outbox deliverer calls it: anything
// else enqueues an IGitHubOutbox entry. Nothing here throws for a GitHub or network failure; the result says what to do next.
public interface IGitHubIssues
{
    // ListRecentCommentsAsync pages through at most this many comments (D15).
    const int MaxListedComments = 500;

    // POST a comment. Ok carries the new comment's id. Ambiguous means GitHub may have made the comment (a timeout, or a 5xx after
    // the body was sent): list the issue's comments and look for the outbox marker before posting again.
    Task<GitHubWriteResult> CommentAsync(IssueRef issue, string body, CancellationToken cancellationToken = default);

    // Close the issue with GitHub's state_reason (completed or not_planned). Ok carries no id. Closing a closed issue is Ok.
    Task<GitHubWriteResult> CloseAsync(IssueRef issue, CloseReason reason, CancellationToken cancellationToken = default);

    // Reopen the issue (state_reason reopened). Ok carries no id.
    Task<GitHubWriteResult> ReopenAsync(IssueRef issue, CancellationToken cancellationToken = default);

    // The issue's comments updated after `since`, oldest first.
    Task<GitHubCommentList> ListRecentCommentsAsync(IssueRef issue, DateTimeOffset since, CancellationToken cancellationToken = default);
}
