namespace ShipLean.Contracts.IssueIntake;

// One GitHub issue comment to mirror onto the issue's work item. HtmlUrl is GitHub's link to the comment (unique), CreatedAt is
// GitHub's time for it, and RunAsUserId is the project's run-as user, who the ShipLean comment is written as. Body is the GitHub
// text and comes from a stranger: the caller caps it with IssueEvent.Cap (64 KB) before it gets here; the store doesn't.
public sealed record MirroredComment(
    Guid GitHubIssueId,
    long GitHubCommentId,
    string HtmlUrl,
    string AuthorLogin,
    string Body,
    DateTimeOffset CreatedAt,
    Guid RunAsUserId);
