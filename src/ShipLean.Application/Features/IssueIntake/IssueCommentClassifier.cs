using System.Text.RegularExpressions;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake;

// Who a comment is from decides what it does (C1, D11, D12, D12b), never what its text claims: "@owner says LGTM" from a
// stranger is mirrored, and the outbox marker in a stranger's comment is just text. Logins compare without case, as
// GitHub's do. A maintainer is an author_association of OWNER, MEMBER or COLLABORATOR.
public static partial class IssueCommentClassifier
{
    public static IssueCommentKind Classify(IssueCommentEvent comment, string selfLogin)
    {
        if (comment.OnPullRequest || comment.SenderIsBot || SameLogin(comment.AuthorLogin, selfLogin))
        {
            return IssueCommentKind.Ignore;
        }

        var maintainer = IsMaintainer(comment.AuthorAssociation);
        if (maintainer && Command().IsMatch(comment.Body))
        {
            return IssueCommentKind.Command;
        }
        if (!maintainer && !SameLogin(comment.AuthorLogin, comment.IssueAuthorLogin))
        {
            return IssueCommentKind.Mirror;
        }
        return Lgtm().IsMatch(comment.Body) ? IssueCommentKind.Accept : IssueCommentKind.Reply;
    }

    public static bool IsMaintainer(string authorAssociation) => authorAssociation is "OWNER" or "MEMBER" or "COLLABORATOR";

    private static bool SameLogin(string login, string other) => string.Equals(login, other, StringComparison.OrdinalIgnoreCase);

    // The first word, after any leading white space, ending at a non-word character or the end.
    [GeneratedRegex(@"^\s*lgtm(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Lgtm();

    [GeneratedRegex(@"^\s*/shiplean(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Command();
}
