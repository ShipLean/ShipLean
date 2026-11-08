namespace ShipLean.Contracts.IssueIntake;

// One GitHub "issue_comment" delivery with action created, parsed from the payload, with the issue it is on. OnPullRequest
// is true when the issue is a pull request (issue.pull_request is present); the classifier ignores those. SenderIsBot is
// true when the sender or the comment's author is a GitHub "Bot" user. Body comes from strangers and is capped like
// IssueEvent.Body; only Author and AuthorAssociation decide who wrote it, never the text.
public sealed record IssueCommentEvent(
    IssueRef Issue,
    string IssueUrl,
    string IssueTitle,
    string IssueAuthorLogin,
    bool IssueOpen,
    bool OnPullRequest,
    long CommentId,
    string CommentUrl,
    string Body,
    string AuthorLogin,
    string AuthorAssociation,
    bool SenderIsBot,
    DateTimeOffset CreatedAt)
{
    // How history and WriteOptions.FromWebhook name the issue: "GitHub owner/name#12".
    public string Reference => $"GitHub {Issue}";

    // The agent name mirrored comments carry (ICommentStore.AddAsAgentAsync): "GitHub @login".
    public string AgentName => $"GitHub @{AuthorLogin}";
}
