using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.IssueIntake.GitHub;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Publish;

// IImagePublications over the GitHub REST API (D14), through GitHubIssuesClient so the instance token has one path.
// 1. GET /commits/{sha}: its committer date is when the fix reached main (a squash or merge on GitHub writes it then).
// 2. GET /actions/workflows/docker-publish.yml/runs?branch=main&event=push&created=>=date: the publish runs since then (a 404 means
//    the repository has no such workflow: NotApplicable).
// 3. The successful runs, oldest first, at most MaxChecked: the first whose head_sha is the fix, or whose GET /compare/{fix}...{head}
//    status is ahead or identical, is Published. A behind or diverged head doesn't contain the fix (another branch history).
// 4. Otherwise a run for the fix that hasn't completed is Pending, and one that completed without success is Failed.
// Anything GitHub doesn't answer is Unknown, so the caller asks again later. Published and NotApplicable are kept for CacheFor.
public sealed class GitHubImagePublications(IServiceScopeFactory scopes, TimeProvider clock) : IImagePublications
{
    public const string Workflow = "docker-publish.yml";
    public const int MaxChecked = 20;
    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    // A run is created when the push lands, a little after the committer date; this covers clocks that are slightly off.
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(10);

    // GitHub returns at most 1,000 filtered runs (10 pages of 100); a run object is a few KB, a compare page holds up to 300 files.
    private const int PageSize = 100;
    private const int MaxPages = 10;
    private const int MaxBody = 16 * 1024 * 1024;

    private static readonly string[] FailedConclusions = ["failure", "timed_out", "cancelled", "startup_failure"];

    private readonly ConcurrentDictionary<string, Cached> _cache = new(StringComparer.Ordinal);

    public async Task<ImagePublication> FindAsync(IssueRef repository, string sha, CancellationToken cancellationToken = default)
    {
        var key = $"{repository.Repository.ToLowerInvariant()}@{sha.ToLowerInvariant()}";
        var now = clock.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && now < cached.Until)
        {
            return cached.Answer;
        }

        // The client reads the scoped integration settings, so it is resolved in a scope of its own.
        await using var scope = scopes.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<GitHubIssuesClient>();
        var answer = await AskAsync(client, repository, sha, cancellationToken);
        if (answer is ImagePublication.Published or ImagePublication.NotApplicable)
        {
            foreach (var expired in _cache.Where(e => now >= e.Value.Until).Select(e => e.Key).ToList())
            {
                _cache.TryRemove(expired, out _);
            }

            _cache[key] = new Cached(answer, now + CacheFor);
        }

        return answer;
    }

    private static async Task<ImagePublication> AskAsync(GitHubIssuesClient client, IssueRef repository, string sha, CancellationToken cancellationToken)
    {
        var escapedSha = Uri.EscapeDataString(sha);
        var (commitResult, commit) = await client.GetRepositoryAsync<CommitReply>(repository, $"commits/{escapedSha}", MaxBody, cancellationToken);
        if (commitResult is not GitHubWriteResult.Ok || commit?.Commit?.Committer?.Date is not { } committedAt)
        {
            return new ImagePublication.Unknown();
        }

        var (stop, runs) = await ListRunsAsync(client, repository, committedAt - Margin, cancellationToken);
        if (stop is not null)
        {
            return stop;
        }

        var ordered = runs.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
        foreach (var run in ordered.Where(r => r is { Status: "completed", Conclusion: "success" }).Take(MaxChecked))
        {
            var contains = await ContainsAsync(client, repository, sha, run.HeadSha, cancellationToken);
            switch (contains)
            {
                case null:
                    // An unanswered compare can't be skipped: a later run would then be reported as the first.
                    return new ImagePublication.Unknown();
                case true:
                    return new ImagePublication.Published(run.UpdatedAt, run.HtmlUrl, run.HeadSha);
            }
        }

        var fixRuns = ordered.Where(r => string.Equals(r.HeadSha, sha, StringComparison.OrdinalIgnoreCase)).ToList();
        if (fixRuns.Exists(r => r.Status != "completed"))
        {
            return new ImagePublication.Pending();
        }

        return fixRuns.LastOrDefault(r => FailedConclusions.Contains(r.Conclusion)) is { } failed
            ? new ImagePublication.Failed(failed.HtmlUrl)
            : new ImagePublication.Unknown();
    }

    // The push runs on main created since `since`; or Stop: NotApplicable when the repository has no such workflow, Unknown when
    // GitHub didn't answer every page.
    private static async Task<(ImagePublication? Stop, List<RunReply> Runs)> ListRunsAsync(GitHubIssuesClient client, IssueRef repository, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var created = Uri.EscapeDataString(">=" + since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        var runs = new List<RunReply>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var (result, reply) = await client.GetRepositoryAsync<RunsReply>(
                repository,
                $"actions/workflows/{Workflow}/runs?branch=main&event=push&exclude_pull_requests=true&created={created}&per_page={PageSize}&page={page}",
                MaxBody,
                cancellationToken);
            if (result is GitHubWriteResult.Failed { Error: "404" })
            {
                return (new ImagePublication.NotApplicable(), runs);
            }

            if (result is not GitHubWriteResult.Ok || reply?.WorkflowRuns is not { } pageRuns)
            {
                return (new ImagePublication.Unknown(), runs);
            }

            runs.AddRange(pageRuns.Where(r => !string.IsNullOrEmpty(r.HeadSha) && !string.IsNullOrEmpty(r.HtmlUrl)));
            if (pageRuns.Count < PageSize)
            {
                break;
            }
        }

        return (null, runs);
    }

    // Whether head contains fix (compare status ahead or identical), or null when GitHub didn't say.
    private static async Task<bool?> ContainsAsync(GitHubIssuesClient client, IssueRef repository, string fix, string head, CancellationToken cancellationToken)
    {
        if (string.Equals(fix, head, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var (result, reply) = await client.GetRepositoryAsync<CompareReply>(
            repository,
            $"compare/{Uri.EscapeDataString(fix)}...{Uri.EscapeDataString(head)}?per_page=1",
            MaxBody,
            cancellationToken);
        return result is GitHubWriteResult.Ok ? reply?.Status switch
        {
            "ahead" or "identical" => true,
            "behind" or "diverged" => false,
            _ => null,
        } : null;
    }

    private sealed record Cached(ImagePublication Answer, DateTimeOffset Until);

    private sealed record CommitReply(CommitBody? Commit);

    private sealed record CommitBody(CommitPerson? Committer);

    private sealed record CommitPerson(DateTimeOffset? Date);

    private sealed record RunsReply(List<RunReply>? WorkflowRuns);

    private sealed record RunReply(long Id, string HeadSha, string? Status, string? Conclusion, string HtmlUrl, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private sealed record CompareReply(string? Status);
}
