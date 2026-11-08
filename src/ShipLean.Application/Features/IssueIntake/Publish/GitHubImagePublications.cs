using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.IssueIntake.GitHub;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Publish;

// IImagePublications over the GitHub REST API (D14), through GitHubIssuesClient so the instance token has one path.
// The rule behind every step: never report a run that might not be the first one; answer Unknown instead.
// 1. GET /commits/{sha}: the full sha (used for every match, so an abbreviated input works) and its committer date. Callers pass the
//    merge or squash commit, which GitHub dates at merge time; Margin covers a little clock skew on top of that assumption.
// 2. GET /actions/workflows/docker-publish.yml/runs?branch=main&event=push&created=>=date: the publish runs since then (a 404 means
//    the repository has no such workflow: NotApplicable). A full last page (10 x 100) means more runs may exist: Unknown.
// 3. The successful completed runs, first to finish first (updated_at of a completed run, then id), at most MaxChecked: the first
//    whose head_sha is the fix, or whose GET /compare/{fix}...{head} status is ahead or identical, is Published, at its completion
//    time. A behind or diverged head doesn't contain the fix. Candidates beyond MaxChecked left unchecked: Unknown.
// 4. Otherwise a run for the fix that hasn't completed is Pending, and one that completed without success is Failed.
// Anything GitHub doesn't answer is Unknown. Answers are kept per (repository, sha): Published 24 h, NotApplicable 1 h, the rest
// 2 min. Concurrent lookups of the same key share one in-flight Task; a failed Task is dropped.
public sealed class GitHubImagePublications(IServiceScopeFactory scopes, TimeProvider clock) : IImagePublications
{
    public const string Workflow = "docker-publish.yml";
    public const int MaxChecked = 20;
    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);
    public static readonly TimeSpan NotApplicableFor = TimeSpan.FromHours(1);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(2);

    // A run is created when the push lands, at or after the merge commit's date; this covers clocks that are off.
    private static readonly TimeSpan Margin = TimeSpan.FromHours(1);

    // GitHub returns at most 1,000 filtered runs (10 pages of 100); a run object is a few KB, a compare page holds up to 300 files.
    private const int PageSize = 100;
    private const int MaxPages = 10;
    private const int MaxBody = 16 * 1024 * 1024;

    private static readonly string[] FailedConclusions = ["failure", "timed_out", "cancelled", "startup_failure"];

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async Task<ImagePublication> FindAsync(IssueRef repository, string sha, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{repository.Repository.ToLowerInvariant()}@{sha.ToLowerInvariant()}";
        Entry entry;
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (!_entries.TryGetValue(key, out entry!) || entry.IsExpired(now))
            {
                foreach (var expired in _entries.Where(e => e.Value.IsExpired(now)).Select(e => e.Key).ToList())
                {
                    _entries.Remove(expired);
                }

                entry = new Entry();
                _entries[key] = entry;
                entry.Answer = LoadAsync(key, entry, repository, sha);
            }
        }

        return await entry.Answer;
    }

    // The shared lookup does not take one caller's token: another caller is waiting on the same Task.
    private async Task<ImagePublication> LoadAsync(string key, Entry entry, IssueRef repository, string sha)
    {
        try
        {
            // The client reads the scoped integration settings, so it is resolved in a scope of its own.
            await using var scope = scopes.CreateAsyncScope();
            var client = scope.ServiceProvider.GetRequiredService<GitHubIssuesClient>();
            var answer = await AskAsync(client, repository, sha, CancellationToken.None);
            lock (_gate)
            {
                entry.Until = clock.GetUtcNow() + answer switch
                {
                    ImagePublication.Published => CacheFor,
                    ImagePublication.NotApplicable => NotApplicableFor,
                    _ => RetryAfter,
                };
            }

            return answer;
        }
        catch (Exception) when (Forget(key, entry))
        {
            throw;
        }
    }

    private bool Forget(string key, Entry entry)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
            }
        }

        return false;
    }

    private static async Task<ImagePublication> AskAsync(GitHubIssuesClient client, IssueRef repository, string sha, CancellationToken cancellationToken)
    {
        var (commitResult, commit) = await client.GetRepositoryAsync<CommitReply>(repository, $"commits/{Uri.EscapeDataString(sha)}", MaxBody, cancellationToken);
        if (commitResult is not GitHubWriteResult.Ok || commit?.Commit?.Committer?.Date is not { } committedAt || string.IsNullOrEmpty(commit.Sha))
        {
            return new ImagePublication.Unknown();
        }

        var fix = commit.Sha;
        var (stop, runs) = await ListRunsAsync(client, repository, committedAt - Margin, cancellationToken);
        if (stop is not null)
        {
            return stop;
        }

        var finished = runs.Where(r => r is { Status: "completed", Conclusion: "success" }).OrderBy(r => r.UpdatedAt).ThenBy(r => r.Id).ToList();
        foreach (var run in finished.Take(MaxChecked))
        {
            var contains = await ContainsAsync(client, repository, fix, run.HeadSha, cancellationToken);
            switch (contains)
            {
                case null:
                    // An unanswered compare can't be skipped: a later run would then be reported as the first.
                    return new ImagePublication.Unknown();
                case true:
                    return new ImagePublication.Published(run.UpdatedAt, run.HtmlUrl, run.HeadSha);
            }
        }

        if (finished.Count > MaxChecked)
        {
            // Unchecked candidates may contain the fix, so neither Failed nor "not yet" is known.
            return new ImagePublication.Unknown();
        }

        var fixRuns = runs.Where(r => string.Equals(r.HeadSha, fix, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
        if (fixRuns.Exists(r => r.Status != "completed"))
        {
            return new ImagePublication.Pending();
        }

        return fixRuns.LastOrDefault(r => FailedConclusions.Contains(r.Conclusion)) is { } failed
            ? new ImagePublication.Failed(failed.HtmlUrl)
            : new ImagePublication.Unknown();
    }

    // The push runs on main created since `since`; or Stop: NotApplicable when the repository has no such workflow, Unknown when
    // GitHub didn't answer every page or the last page was full (more runs may exist).
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
                return (null, runs);
            }
        }

        // The last page was full: runs beyond it may be the first to publish the fix.
        return (new ImagePublication.Unknown(), runs);
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

    private sealed class Entry
    {
        public Task<ImagePublication> Answer { get; set; } = null!;

        // Null while the lookup is in flight.
        public DateTimeOffset? Until { get; set; }

        public bool IsExpired(DateTimeOffset now) => Until is { } until && now >= until;
    }

    private sealed record CommitReply(string? Sha, CommitBody? Commit);

    private sealed record CommitBody(CommitPerson? Committer);

    private sealed record CommitPerson(DateTimeOffset? Date);

    private sealed record RunsReply(List<RunReply>? WorkflowRuns);

    private sealed record RunReply(long Id, string HeadSha, string? Status, string? Conclusion, string HtmlUrl, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private sealed record CompareReply(string? Status);
}
