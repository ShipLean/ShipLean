namespace ShipLean.Contracts.IssueIntake;

// The conversation lane. ReplyAsync is a comment from the issue author or a maintainer that isn't LGTM or "/shiplean":
// it is mirrored and may start a round (D11). MirrorAsync is anyone else's comment: it is only mirrored. Both are
// idempotent (a mirrored comment is found by its GitHub URL before it is added).
public interface IIssueReplies
{
    Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment, CancellationToken cancellationToken);

    Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment, CancellationToken cancellationToken);
}
