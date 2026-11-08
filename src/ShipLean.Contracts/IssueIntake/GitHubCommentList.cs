namespace ShipLean.Contracts.IssueIntake;

// The answer of IGitHubIssues.ListRecentCommentsAsync. Listed: the comments, oldest first; Complete is false when there were more
// than IGitHubIssues.MaxListedComments. Retry and Failed mean the same as for a GitHubWriteResult (a GET is never Ambiguous).
public abstract record GitHubCommentList
{
    private GitHubCommentList()
    {
    }

    public sealed record Listed(IReadOnlyList<GitHubComment> Comments, bool Complete) : GitHubCommentList;

    public sealed record Retry(string Error) : GitHubCommentList;

    public sealed record Failed(string Error) : GitHubCommentList;
}
