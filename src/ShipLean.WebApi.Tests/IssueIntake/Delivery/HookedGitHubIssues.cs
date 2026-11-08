using ShipLean.Contracts.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.Delivery;

// Wraps an IGitHubIssues and runs a test's hook once, at a set point: before the next comment reaches GitHub, after it did, or
// before the next list. A hook that throws is a worker dying there; one that waits is a worker stalled there.
internal sealed class HookedGitHubIssues(IGitHubIssues inner) : IGitHubIssues
{
    private Func<Task>? _beforeComment;
    private Func<Task>? _afterComment;
    private Func<Task>? _beforeList;

    public HookedGitHubIssues BeforeComment(Func<Task> hook)
    {
        _beforeComment = hook;
        return this;
    }

    public HookedGitHubIssues AfterComment(Func<Task> hook)
    {
        _afterComment = hook;
        return this;
    }

    public HookedGitHubIssues BeforeList(Func<Task> hook)
    {
        _beforeList = hook;
        return this;
    }

    public async Task<GitHubWriteResult> CommentAsync(IssueRef issue, string body, CancellationToken cancellationToken = default)
    {
        await RunOnceAsync(ref _beforeComment);
        var result = await inner.CommentAsync(issue, body, cancellationToken);
        await RunOnceAsync(ref _afterComment);
        return result;
    }

    public Task<GitHubWriteResult> CloseAsync(IssueRef issue, CloseReason reason, CancellationToken cancellationToken = default)
    {
        return inner.CloseAsync(issue, reason, cancellationToken);
    }

    public Task<GitHubWriteResult> ReopenAsync(IssueRef issue, CancellationToken cancellationToken = default)
    {
        return inner.ReopenAsync(issue, cancellationToken);
    }

    public async Task<GitHubCommentList> ListRecentCommentsAsync(IssueRef issue, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        await RunOnceAsync(ref _beforeList);
        return await inner.ListRecentCommentsAsync(issue, since, cancellationToken);
    }

    private static Task RunOnceAsync(ref Func<Task>? hook)
    {
        return Interlocked.Exchange(ref hook, null) is { } run ? run() : Task.CompletedTask;
    }
}
