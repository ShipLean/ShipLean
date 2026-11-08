using System.Text;

namespace ShipLean.Contracts.IssueIntake;

// One GitHub "issues" delivery ShipLean acts on (opened, edited, closed or reopened), parsed from the payload. Issue's
// repository is lower case and its host comes from repository.html_url. Title, Body, Labels and the logins come from
// strangers: never let them choose a project, state, type or user, and pass them only as parameters. Body is capped at
// MaxTextBytes UTF-8 bytes, with a note where it was cut (Cap). AuthorAssociation is GitHub's issue.author_association as sent
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
    public const int MaxTextBytes = 64 * 1024;

    public const string TruncatedNote = "\n\n_(ShipLean cut this text: it was longer than 64 KB.)_";

    // Text from strangers is kept to MaxTextBytes once encoded as UTF-8, TruncatedNote included where it was cut. The cut
    // falls between Unicode scalars, so a surrogate pair is never split.
    public static string Cap(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= MaxTextBytes)
        {
            return text;
        }

        var budget = MaxTextBytes - Encoding.UTF8.GetByteCount(TruncatedNote);
        var bytes = 0;
        var chars = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > budget)
            {
                break;
            }
            bytes += rune.Utf8SequenceLength;
            chars += rune.Utf16SequenceLength;
        }
        return string.Concat(text.AsSpan(0, chars), TruncatedNote);
    }

    // How history and WriteOptions.FromWebhook name the issue: "GitHub owner/name#12".
    public string Reference => $"GitHub {Issue}";
}
