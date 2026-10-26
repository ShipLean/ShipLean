using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Mcp.Planning.Markers;

// How a plan marks its comments and links so they can be found again. Comments start with a prefix ("Report · done ...");
// a commit link is titled "commit <sha7> <subject>", a pull request link "PR <title or url>".
public static partial class PlanMarkers
{
    public const string ReportPrefix = "Report · ";
    public const string ReviewPrefix = "Review · ";
    public const string RetroPrefix = "Retro · ";
    public const string CommitPrefix = "commit ";
    public const string PullRequestPrefix = "PR ";

    public static string Report(string text) => ReportPrefix + text;

    public static string Review(string text) => ReviewPrefix + text;

    public static string Retro(string text) => RetroPrefix + text;

    public static bool IsReport(string comment) => comment.StartsWith(ReportPrefix, StringComparison.Ordinal);

    public static bool IsReview(string comment) => comment.StartsWith(ReviewPrefix, StringComparison.Ordinal);

    public static bool IsRetro(string comment) => comment.StartsWith(RetroPrefix, StringComparison.Ordinal);

    // True for what Commit accepts: at least 7 hex characters. Callers check a sha from an agent with this first.
    public static bool IsCommitSha(string? sha) => sha is not null && ShortSha().IsMatch(sha);

    // The sha is cut to its first 7 characters and lowercased. The subject's line breaks become single spaces, so the title
    // is one line.
    public static string Commit(string sha, string subject)
    {
        if (!IsCommitSha(sha))
        {
            throw new ArgumentException($"\"{sha}\" is not a commit sha of at least 7 hex characters.", nameof(sha));
        }
        return $"{CommitPrefix}{sha[..7].ToLowerInvariant()} {LineBreaks().Replace(subject, " ").Trim()}".TrimEnd();
    }

    public static string PullRequest(string titleOrUrl) => PullRequestPrefix + titleOrUrl.Trim();

    public static bool IsCommit(string title) => CommitTitle().IsMatch(title);

    public static bool IsPullRequest(string title) => title.StartsWith(PullRequestPrefix, StringComparison.Ordinal) && title.Length > PullRequestPrefix.Length;

    // Reads a title Commit wrote: the 7-character sha and the subject (empty when there was none).
    public static bool TryReadCommit(string title, out string sha, out string subject)
    {
        var match = CommitTitle().Match(title);
        sha = match.Success ? match.Groups[1].Value : string.Empty;
        subject = match.Success ? match.Groups[2].Value : string.Empty;
        return match.Success;
    }

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineBreaks();

    [GeneratedRegex("^[0-9a-fA-F]{7,64}\\z")]
    private static partial Regex ShortSha();

    [GeneratedRegex("^commit ([0-9a-f]{7})(?: (.*))?\\z", RegexOptions.Singleline)]
    private static partial Regex CommitTitle();
}
