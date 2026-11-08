namespace ShipLean.Contracts.IssueIntake;

// One GitHub "issues" delivery ShipLean acts on (opened, edited, closed or reopened), parsed from the payload. Issue's
// repository is lower case and its host comes from repository.html_url. Title, Body, Labels and the logins come from
// strangers: never let them choose a project, state, type or user, and pass them only as parameters. Body is capped at
// MaxTextLength characters, with a note where it was cut (IssueCommentClassifier.Cap). AuthorAssociation is GitHub's issue.author_association as sent
// (OWNER, MEMBER, COLLABORATOR, CONTRIBUTOR, NONE, ...).
public sealed record IssueEvent(
    IssueAction Action,
    IssueRef Issue,
    string Url,
    string Title,
    string Body,
    string AuthorLogin,
    string AuthorAssociation,
    IReadOnlyList<string> Labels,
    bool Open,
    string? StateReason,
    string SenderLogin,
    bool SenderIsBot,
    DateTimeOffset UpdatedAt)
{
    public const int MaxTextLength = 64 * 1024;

    // How history and WriteOptions.FromWebhook name the issue: "GitHub owner/name#12".
    public string Reference => $"GitHub {Issue}";
}
