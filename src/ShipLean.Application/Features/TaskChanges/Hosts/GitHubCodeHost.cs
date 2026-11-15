using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// Commits and patches from the GitHub REST API (GitHubApiUrl), with the GitHubToken as a bearer when the API address is https.
// GitHub answers 404 for a missing repository and a missing commit alike, so a commit 404 is followed by a read of the
// repository itself: 404 there is Unreadable, anything else Missing.
public sealed class GitHubCodeHost(IHttpClientFactory clients, IIntegrationSettings settings) : ICodeHost
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
    {
        var (reply, status) = await GetAsync(repo, $"commits/{Uri.EscapeDataString(sha)}", diff: false, cancellationToken);
        if (reply is not { Ok: true })
        {
            return new CommitRead(status, null, reply?.Status);
        }

        GitHubCommit? commit;
        try
        {
            commit = JsonSerializer.Deserialize<GitHubCommit>(reply.Body!, Wire);
        }
        catch (JsonException)
        {
            return new CommitRead(CommitReadStatus.Unreadable, null, reply.Status);
        }

        if (commit?.Sha is null)
        {
            return new CommitRead(CommitReadStatus.Unreadable, null, reply.Status);
        }

        var meta = new CommitMeta(
            commit.Sha,
            [.. (commit.Parents ?? []).Select(p => p.Sha)],
            Subject(commit.Commit?.Message),
            commit.Commit?.Author?.Name ?? "",
            commit.Commit?.Author?.Date ?? DateTimeOffset.UnixEpoch,
            commit.Stats?.Additions ?? 0,
            commit.Stats?.Deletions ?? 0,
            [.. (commit.Files ?? []).Select(f => f.Filename)]);
        return new CommitRead(CommitReadStatus.Found, meta, reply.Status);
    }

    public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => PatchAsync(repo, $"commits/{Uri.EscapeDataString(sha)}", cancellationToken);

    public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
        => PatchAsync(repo, $"compare/{Uri.EscapeDataString(baseSha)}...{Uri.EscapeDataString(headSha)}", cancellationToken);

    private async Task<PatchRead> PatchAsync(RepoRef repo, string route, CancellationToken cancellationToken)
    {
        var (reply, status) = await GetAsync(repo, route, diff: true, cancellationToken);
        return reply is { Ok: true } ? new PatchRead(CommitReadStatus.Found, reply.Body) : new PatchRead(status, null);
    }

    // The reply, and the status to report when it is not a success.
    private async Task<(CodeHostReply? Reply, CommitReadStatus Status)> GetAsync(RepoRef repo, string route, bool diff, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync(cancellationToken);
        var root = config.GitHubApiUrl.TrimEnd('/');
        if (!Uri.TryCreate($"{root}/repos/{repo.Path}/{route}", UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            return (null, CommitReadStatus.Unreadable);
        }

        var client = clients.CreateClient(CodeHostServices.GitHubClientName);
        var reply = await CodeHostReply.GetAsync(client, url, Decorate(config, url, diff), cancellationToken);
        if (reply.Ok)
        {
            return (reply, CommitReadStatus.Found);
        }

        int? repositoryStatus = null;
        if (reply.Status == (int)HttpStatusCode.NotFound && Uri.TryCreate($"{root}/repos/{repo.Path}", UriKind.Absolute, out var repoUrl))
        {
            repositoryStatus = (await CodeHostReply.GetAsync(client, repoUrl, Decorate(config, repoUrl, false), cancellationToken)).Status;
        }

        return (reply, CodeHostReply.FailureOf(reply.Status, repositoryStatus));
    }

    private static Action<HttpRequestMessage> Decorate(IntegrationSettingsDto config, Uri url, bool diff) => request =>
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(diff ? "application/vnd.github.diff" : "application/vnd.github+json"));
        if (!string.IsNullOrEmpty(config.GitHubToken) && url.Scheme == Uri.UriSchemeHttps)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GitHubToken);
        }
    };

    private static string Subject(string? message)
    {
        var line = (message ?? "").Split('\n', 2)[0].TrimEnd('\r');
        return CommitSubject.Cut(line);
    }

    private sealed record GitHubCommit(string? Sha, List<GitHubParent>? Parents, GitHubCommitBody? Commit, GitHubStats? Stats, List<GitHubFile>? Files);

    private sealed record GitHubParent(string Sha);

    private sealed record GitHubCommitBody(string? Message, GitHubAuthor? Author);

    private sealed record GitHubAuthor(string? Name, DateTimeOffset? Date);

    private sealed record GitHubStats(int Additions, int Deletions);

    private sealed record GitHubFile(string Filename);
}
