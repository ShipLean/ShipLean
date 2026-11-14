using System.Net;
using System.Text;
using System.Text.Json;
using ShipLean.Application.Features.Links.GitLab;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// Commits and patches from the v4 API of the configured GitLab (GitLabUrl), with the GitLabToken as PRIVATE-TOKEN when that
// address is https. GitLab hands back a diff per file as JSON, so the unified text is rebuilt here, with the git headers.
public sealed class GitLabCodeHost(IHttpClientFactory clients, IIntegrationSettings settings) : ICodeHost
{
    private const int MaxPages = 30;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
    {
        var commitRoute = $"repository/commits/{Uri.EscapeDataString(sha)}";
        var (reply, status) = await GetAsync(repo, commitRoute, cancellationToken);
        if (reply is not { Ok: true })
        {
            return new CommitRead(status, null, reply?.Status);
        }

        GitLabCommit? commit;
        List<GitLabFileDiff> files;
        try
        {
            commit = JsonSerializer.Deserialize<GitLabCommit>(reply.Body!, Wire);
            var diffs = await DiffsAsync(repo, $"{commitRoute}/diff", cancellationToken);
            if (diffs.Files is null)
            {
                return new CommitRead(diffs.Status, null, null);
            }

            files = diffs.Files;
        }
        catch (JsonException)
        {
            return new CommitRead(CommitReadStatus.Unreadable, null, reply.Status);
        }

        if (commit?.Id is null)
        {
            return new CommitRead(CommitReadStatus.Unreadable, null, reply.Status);
        }

        var meta = new CommitMeta(
            commit.Id,
            commit.ParentIds ?? [],
            GitLabLimits.Truncate(commit.Title, GitLabLimits.MaxTitle) ?? "",
            commit.AuthorName ?? "",
            commit.AuthoredDate ?? DateTimeOffset.UnixEpoch,
            commit.Stats?.Additions ?? 0,
            commit.Stats?.Deletions ?? 0,
            [.. files.Select(f => f.DeletedFile ? f.OldPath : f.NewPath)]);
        return new CommitRead(CommitReadStatus.Found, meta, reply.Status);
    }

    public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => PatchAsync(repo, $"repository/commits/{Uri.EscapeDataString(sha)}/diff", wrapped: false, cancellationToken);

    public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
        => PatchAsync(repo, $"repository/compare?from={Uri.EscapeDataString(baseSha)}&to={Uri.EscapeDataString(headSha)}", wrapped: true, cancellationToken);

    // The git unified text of the files: for each, the headers git prints, then GitLab's hunks.
    private static string UnifiedText(IEnumerable<GitLabFileDiff> files)
    {
        var text = new StringBuilder();
        foreach (var file in files)
        {
            text.Append("diff --git a/").Append(file.OldPath).Append(" b/").Append(file.NewPath).Append('\n');
            if (file.NewFile)
            {
                text.Append("new file mode ").Append(Mode(file.BMode)).Append('\n');
            }
            else if (file.DeletedFile)
            {
                text.Append("deleted file mode ").Append(Mode(file.AMode)).Append('\n');
            }
            else if (file.RenamedFile || file.OldPath != file.NewPath)
            {
                if (file.Diff.Length == 0)
                {
                    text.Append("similarity index 100%\n");
                }

                text.Append("rename from ").Append(file.OldPath).Append('\n');
                text.Append("rename to ").Append(file.NewPath).Append('\n');
            }

            if (file.Diff.Length > 0)
            {
                text.Append("--- ").Append(file.NewFile ? "/dev/null" : $"a/{file.OldPath}").Append('\n');
                text.Append("+++ ").Append(file.DeletedFile ? "/dev/null" : $"b/{file.NewPath}").Append('\n');
                text.Append(file.Diff);
                if (!file.Diff.EndsWith('\n'))
                {
                    text.Append('\n');
                }
            }
        }

        return text.ToString();
    }

    private static string Mode(string? mode) => string.IsNullOrEmpty(mode) || mode == "0" ? "100644" : mode;

    private async Task<PatchRead> PatchAsync(RepoRef repo, string route, bool wrapped, CancellationToken cancellationToken)
    {
        try
        {
            var diffs = await DiffsAsync(repo, route, cancellationToken, wrapped);
            return diffs.Files is null ? new PatchRead(diffs.Status, null) : new PatchRead(CommitReadStatus.Found, UnifiedText(diffs.Files));
        }
        catch (JsonException)
        {
            return new PatchRead(CommitReadStatus.Unreadable, null);
        }
    }

    // Every file diff of the route, page after page. Files is null when a page could not be read, with the status to report.
    private async Task<(List<GitLabFileDiff>? Files, CommitReadStatus Status)> DiffsAsync(RepoRef repo, string route, CancellationToken cancellationToken, bool wrapped = false)
    {
        var files = new List<GitLabFileDiff>();
        var separator = route.Contains('?') ? '&' : '?';
        for (var page = 1; page <= MaxPages; page++)
        {
            var (reply, status) = await GetAsync(repo, $"{route}{separator}per_page=100&page={page}", cancellationToken);
            if (reply is not { Ok: true })
            {
                return (null, status);
            }

            files.AddRange(wrapped
                ? JsonSerializer.Deserialize<GitLabCompare>(reply.Body!, Wire)?.Diffs ?? []
                : JsonSerializer.Deserialize<List<GitLabFileDiff>>(reply.Body!, Wire) ?? []);
            if (string.IsNullOrEmpty(reply.NextPage))
            {
                break;
            }
        }

        return (files, CommitReadStatus.Found);
    }

    private async Task<(CodeHostReply? Reply, CommitReadStatus Status)> GetAsync(RepoRef repo, string route, CancellationToken cancellationToken)
    {
        var config = await settings.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(config.GitLabUrl))
        {
            return (null, CommitReadStatus.Unreadable);
        }

        var root = $"{config.GitLabUrl.Trim().TrimEnd('/')}/api/v4/projects/{Uri.EscapeDataString(repo.Path)}";
        if (!Uri.TryCreate($"{root}/{route}", UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            return (null, CommitReadStatus.Unreadable);
        }

        var client = clients.CreateClient(GitLabSource.HttpClientName);
        var reply = await CodeHostReply.GetAsync(client, url, Decorate(config, url), cancellationToken);
        if (reply.Ok)
        {
            return (reply, CommitReadStatus.Found);
        }

        int? projectStatus = null;
        if (reply.Status == (int)HttpStatusCode.NotFound)
        {
            var projectUrl = new Uri(root);
            projectStatus = (await CodeHostReply.GetAsync(client, projectUrl, Decorate(config, projectUrl), cancellationToken)).Status;
        }

        return (reply, CodeHostReply.FailureOf(reply.Status, projectStatus));
    }

    private static Action<HttpRequestMessage> Decorate(IntegrationSettingsDto config, Uri url) => request =>
    {
        if (!string.IsNullOrEmpty(config.GitLabToken) && url.Scheme == Uri.UriSchemeHttps)
        {
            request.Headers.Add("PRIVATE-TOKEN", config.GitLabToken);
        }
    };

    private sealed record GitLabCommit(string? Id, List<string>? ParentIds, string? Title, string? AuthorName, DateTimeOffset? AuthoredDate, GitLabStats? Stats);

    private sealed record GitLabStats(int Additions, int Deletions);

    private sealed record GitLabCompare(List<GitLabFileDiff>? Diffs);

    private sealed record GitLabFileDiff(string OldPath, string NewPath, string? AMode, string? BMode, bool NewFile, bool RenamedFile, bool DeletedFile, string Diff);
}
