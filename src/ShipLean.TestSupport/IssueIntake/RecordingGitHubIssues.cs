using System.Collections.Concurrent;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IGitHubIssues that calls nothing: it records every call as "comment owner/name#n", "close owner/name#n notPlanned",
// "reopen owner/name#n" or "list owner/name#n", answers with the results a test queues (Ok with the next id when none is
// queued), and lists the comments it was given or Ok-commented.
public sealed class RecordingGitHubIssues : IGitHubIssues
{
    private readonly ConcurrentQueue<string> _calls = new();
    private readonly ConcurrentQueue<string> _bodies = new();
    private readonly ConcurrentQueue<GitHubWriteResult> _results = new();
    private readonly ConcurrentQueue<GitHubCommentList> _lists = new();
    private readonly ConcurrentDictionary<IssueRef, ConcurrentQueue<GitHubComment>> _comments = new();
    private long _nextCommentId = 1000;

    public IReadOnlyList<string> Calls => [.. _calls];

    // Bodies of every comment asked for, in order.
    public IReadOnlyList<string> CommentBodies => [.. _bodies];

    public string SelfLogin { get; set; } = FixedGitHubIdentity.DefaultLogin;

    // The next write returns this instead of Ok.
    public void QueueResult(GitHubWriteResult result) => _results.Enqueue(result);

    // The next list returns this instead of the recorded comments.
    public void QueueList(GitHubCommentList list) => _lists.Enqueue(list);

    // A comment already on the issue (for example one an ambiguous POST made).
    public void AddExistingComment(IssueRef issue, GitHubComment comment) => _comments.GetOrAdd(issue, _ => new()).Enqueue(comment);

    public Task<GitHubWriteResult> CommentAsync(IssueRef issue, string body, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue($"comment {issue}");
        _bodies.Enqueue(body);
        if (_results.TryDequeue(out var queued))
        {
            return Task.FromResult(queued);
        }

        var id = Interlocked.Increment(ref _nextCommentId);
        AddExistingComment(issue, new GitHubComment(id, SelfLogin, body, TimeProvider.System.GetUtcNow()));
        return Task.FromResult<GitHubWriteResult>(new GitHubWriteResult.Ok(id));
    }

    public Task<GitHubWriteResult> CloseAsync(IssueRef issue, CloseReason reason, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue($"close {issue} {(reason == CloseReason.Completed ? "completed" : "notPlanned")}");
        return Task.FromResult(_results.TryDequeue(out var queued) ? queued : new GitHubWriteResult.Ok(null));
    }

    public Task<GitHubWriteResult> ReopenAsync(IssueRef issue, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue($"reopen {issue}");
        return Task.FromResult(_results.TryDequeue(out var queued) ? queued : new GitHubWriteResult.Ok(null));
    }

    public Task<GitHubCommentList> ListRecentCommentsAsync(IssueRef issue, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue($"list {issue}");
        if (_lists.TryDequeue(out var queued))
        {
            return Task.FromResult(queued);
        }

        IReadOnlyList<GitHubComment> comments = _comments.TryGetValue(issue, out var found) ? [.. found.Where(c => c.CreatedAt >= since)] : [];
        return Task.FromResult<GitHubCommentList>(new GitHubCommentList.Listed(comments, Complete: true));
    }
}
