using ShipLean.Contracts.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.Delivery;

// Wraps an IGitHubIssues so the first call waits until the test releases it: a worker held mid-send, while another worker runs.
// Entered completes when that first call arrives. Every later call goes straight through.
internal sealed class GatedGitHubIssues(IGitHubIssues inner) : IGitHubIssues
{
    private int _calls;

    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<GitHubWriteResult> CommentAsync(IssueRef issue, string body, CancellationToken cancellationToken = default)
    {
        await WaitIfFirstAsync();
        return await inner.CommentAsync(issue, body, cancellationToken);
    }

    public async Task<GitHubWriteResult> CloseAsync(IssueRef issue, CloseReason reason, CancellationToken cancellationToken = default)
    {
        await WaitIfFirstAsync();
        return await inner.CloseAsync(issue, reason, cancellationToken);
    }

    public async Task<GitHubWriteResult> ReopenAsync(IssueRef issue, CancellationToken cancellationToken = default)
    {
        await WaitIfFirstAsync();
        return await inner.ReopenAsync(issue, cancellationToken);
    }

    public async Task<GitHubCommentList> ListRecentCommentsAsync(IssueRef issue, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        await WaitIfFirstAsync();
        return await inner.ListRecentCommentsAsync(issue, since, cancellationToken);
    }

    private async Task WaitIfFirstAsync()
    {
        if (Interlocked.Increment(ref _calls) == 1)
        {
            Entered.SetResult();
            await Release.Task;
        }
    }
}
