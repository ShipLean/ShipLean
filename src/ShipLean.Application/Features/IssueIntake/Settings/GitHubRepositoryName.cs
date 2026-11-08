using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.IssueIntake.Settings;

// The "owner/name" a project's repo_url names on github.com, or null when it names anything else (another host, a deeper
// path, characters GitHub does not allow). Accepts https://github.com/o/n, ssh://git@github.com/o/n and git@github.com:o/n,
// each with or without .git and a trailing slash.
internal static partial class GitHubRepositoryName
{
    public static string? FromRepoUrl(string? repoUrl)
    {
        var text = repoUrl?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        string path;
        if (Scp().Match(text) is { Success: true } scp)
        {
            path = scp.Groups["path"].Value;
        }
        else if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "ssh" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            path = uri.AbsolutePath;
        }
        else
        {
            return null;
        }

        var segments = path.Trim('/').Split('/');
        if (segments.Length != 2)
        {
            return null;
        }

        var name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        return Part().IsMatch(segments[0]) && Part().IsMatch(name) && name is not ("." or "..") ? $"{segments[0]}/{name}" : null;
    }

    [GeneratedRegex(@"^git@github\.com:(?<path>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Scp();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex Part();
}
