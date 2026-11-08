namespace ShipLean.Contracts.IssueIntake;

// One GitHub "issues" delivery ShipLean acts on (opened, edited, closed or reopened), parsed from the payload. Issue's
// repository is lower case and its host comes from repository.html_url. Title, Body, Labels and the logins come from
// strangers: never let them choose a project, state, type or user, and pass them only as parameters. Body is capped at
// MaxTextLength characters, with a note where it was cut (Cap). AuthorAssociation is GitHub's issue.author_association as sent
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

    public const string TruncatedNote = "\n\n_(ShipLean cut this text: it was longer than 64 KB.)_";

    // Text from strangers is kept to MaxTextLength characters, with TruncatedNote where it was cut.
    public static string Cap(string text)
        => text.Length <= MaxTextLength ? text : string.Concat(text.AsSpan(0, MaxTextLength - TruncatedNote.Length), TruncatedNote);

    // How history and WriteOptions.FromWebhook name the issue: "GitHub owner/name#12".
    public string Reference => $"GitHub {Issue}";
}
