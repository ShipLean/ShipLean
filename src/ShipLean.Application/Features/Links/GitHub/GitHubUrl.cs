using System.Text.RegularExpressions;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.GitHub;

// A github.com pull request or issue address: https://github.com/<owner>/<repo>/pull/<n> or /issues/<n>. Owner and repo are
// kept to the characters GitHub allows, so a crafted path can't steer the API call somewhere else.
public sealed partial record GitHubUrl(LinkKind Kind, string Owner, string Repo, int Number)
{
    public const string Host = "github.com";

    public string Location => $"{Owner}/{Repo}";

    // The API path after the base address: repos/<owner>/<repo>/pulls/<n> or .../issues/<n>.
    public string ApiPath => $"repos/{Owner}/{Repo}/{(Kind == LinkKind.GithubPull ? "pulls" : "issues")}/{Number}";

    public static bool TryParse(Uri url, out GitHubUrl parsed)
    {
        parsed = null!;
        if (url.Scheme is not ("http" or "https") || !url.Host.Equals(Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not [var owner, var repo, var what, var number, ..] || !int.TryParse(number, out var n) || n <= 0
            || !IsName(owner) || !IsName(repo))
        {
            return false;
        }

        var kind = what switch { "pull" => LinkKind.GithubPull, "issues" => LinkKind.GithubIssue, _ => LinkKind.Page };
        if (kind == LinkKind.Page)
        {
            return false;
        }

        parsed = new GitHubUrl(kind, owner, repo, n);
        return true;
    }

    private static bool IsName(string segment) => segment is not ("." or "..") && NamePattern().IsMatch(segment);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex NamePattern();
}
