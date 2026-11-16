using System.Text.RegularExpressions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Repos;

// Reads a repository or commit address in any of the forms people paste, and decides GitHub or GitLab from the instance's
// integration settings: github.com or the GitHub API host is GitHub, the GitLab address host is GitLab, any other host is refused.
public sealed partial class RepoAddresses(IIntegrationSettings settings) : IRepoAddresses
{
    private const string BadRepo = "Send the repository as owner/name, a clone address or a web address.";

    private const string BadSha = "Send the full sha or a commit URL";

    public async Task<RepoRef> ParseAsync(string repo, CancellationToken cancellationToken = default)
    {
        var (host, segments) = Split(repo, "repo", BadRepo);
        return await ResolveAsync(host, segments, "repo", BadRepo, cancellationToken);
    }

    public async Task<CommitUrlParts> ParseCommitUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var trimmed = url?.Trim() ?? string.Empty;
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Validation(BadSha, "sha");
        }

        var (host, segments) = Split(trimmed, "sha", BadSha);
        if (segments.Count < 4 || !ShaPattern().IsMatch(segments[^1]) || segments[^2] != "commit")
        {
            throw ApiException.Validation(BadSha, "sha");
        }

        var gitlabForm = segments.Count >= 5 && segments[^3] == "-";
        var repoSegments = segments.Take(segments.Count - (gitlabForm ? 3 : 2)).ToList();
        var repo = await ResolveAsync(host, repoSegments, "sha", BadSha, cancellationToken);
        if (gitlabForm != (repo.Provider == CodeProvider.Gitlab))
        {
            throw ApiException.Validation(BadSha, "sha");
        }

        return new CommitUrlParts(repo, segments[^1].ToLowerInvariant());
    }

    private async Task<RepoRef> ResolveAsync(string host, List<string> segments, string field, string message, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync(cancellationToken);
        var provider = ProviderOf(host, config) ?? throw ApiException.RepoUnreadable($"ShipLean has no GitHub or GitLab connection for {host}");
        if (segments.Count < 2 || (provider == CodeProvider.Github && segments.Count != 2) || segments.Any(s => !SegmentPattern().IsMatch(s)))
        {
            throw ApiException.Validation(message, field);
        }

        return new RepoRef(provider, host, string.Join('/', segments));
    }

    private static CodeProvider? ProviderOf(string host, IntegrationSettingsDto config)
    {
        if (host == "github.com" || host == HostOf(config.GitHubApiUrl))
        {
            return CodeProvider.Github;
        }

        return config.GitLabUrl is { Length: > 0 } gitlab && host == HostOf(gitlab) ? CodeProvider.Gitlab : null;
    }

    private static string? HostOf(string address)
    {
        var withScheme = address.Contains("://", StringComparison.Ordinal) ? address : $"https://{address}";
        return Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : null;
    }

    // The lower-case host and the path segments, with a trailing .git, query and fragment dropped.
    private static (string Host, List<string> Segments) Split(string input, string field, string message)
    {
        var text = input?.Trim() ?? string.Empty;
        string host;
        string path;
        var scp = ScpPattern().Match(text);
        if (scp.Success)
        {
            host = scp.Groups["host"].Value;
            path = scp.Groups["path"].Value;
        }
        else
        {
            var withScheme = text.Contains("://", StringComparison.Ordinal) ? text : $"https://{text}";
            if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "ssh" or "git") || uri.Host.Length == 0)
            {
                throw ApiException.Validation(message, field);
            }

            host = uri.Host;
            path = uri.AbsolutePath;
        }

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant()).ToList();
        if (segments.Count > 0 && segments[^1].EndsWith(".git", StringComparison.Ordinal))
        {
            segments[^1] = segments[^1][..^4];
        }

        return (host.ToLowerInvariant(), segments);
    }

    [GeneratedRegex(@"^(?:[^@/\s]+@)?(?<host>[A-Za-z0-9.-]+):(?<path>(?!//)[^\s]+)$")]
    private static partial Regex ScpPattern();

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$")]
    private static partial Regex ShaPattern();

    [GeneratedRegex(@"^[a-z0-9._-]+$")]
    private static partial Regex SegmentPattern();
}
