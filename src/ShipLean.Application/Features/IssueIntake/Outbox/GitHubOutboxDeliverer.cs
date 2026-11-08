using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Outbox;

// Delivers github_outbox (D15): one entry at a time per issue, in seq order, each claimed with a lease (OutboxClaims).
// - A comment carries its entry's marker, added at send time. While the issue is closed on GitHub a comment is not sent (it fails
//   with "issue closed"); closes and reopens still go.
// - After an ambiguous POST the next try first lists the issue's comments since the entry was made and marks it sent only when a
//   comment by ShipLean's own login carries the marker (anyone can type one). Not found: it posts again.
// - Retry and Ambiguous back off 1 min, 10 min, 1 h, then 6 h, and fail after MaxAttempts claims.
// - A write that fails for good also fails the issue's next entry when it is a close (the close is held), and leaves one ShipLean
//   comment on the issue's item for the maintainers.
// Bodies are never logged. The hosted loop is GitHubOutboxDeliveryService; tests drive a pass directly with their own clock.
public sealed class GitHubOutboxDeliverer(
    NpgsqlDataSource dataSource, IServiceScopeFactory scopes, IGitHubIdentity identity, TimeProvider clock, ILogger<GitHubOutboxDeliverer> logger)
{
    public const int MaxAttempts = 8;
    public const string NoteAgentName = "ShipLean";

    // How long a claim holds its entry (and so its issue) before another worker may take it over.
    public static readonly TimeSpan Lease = OutboxClaims.Lease;

    private const string ClosedError = "issue closed";
    private const string HeldError = "previous update failed";

    // A margin under the entry's created_at for the reconcile list, against clock skew between ShipLean and GitHub.
    private static readonly TimeSpan ListMargin = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromHours(6)];

    // The wait after a try that was not done, given the claims made so far; null when the entry has had its last try.
    public static TimeSpan? RetryAfter(int attempts)
        => attempts >= MaxAttempts ? null : Backoff[Math.Clamp(attempts, 1, Backoff.Length) - 1];

    // Delivers every entry that is due now. Returns how many it tried.
    public async Task<int> RunDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var github = scope.ServiceProvider.GetRequiredService<IGitHubIssues>();
        var tried = 0;
        while (await OutboxClaims.ClaimNextAsync(dataSource, clock.GetUtcNow(), cancellationToken) is { } claim)
        {
            await DeliverAsync(claim, github, scope.ServiceProvider, cancellationToken);
            tried++;
        }

        return tried;
    }

    private async Task DeliverAsync(OutboxClaim claim, IGitHubIssues github, IServiceProvider services, CancellationToken cancellationToken)
    {
        var issue = new IssueRef(claim.Host, claim.Repository, claim.Number);
        if (claim.Attempts > MaxAttempts)
        {
            await FailAsync(claim, issue, "retry", "the last try's lease expired", services, cancellationToken);
            return;
        }

        if (claim.Kind == "comment" && !claim.OpenOnGitHub)
        {
            await FailQuietlyAsync(claim, issue, cancellationToken);
            return;
        }

        if (claim.Kind == "comment" && claim.LastResult == "ambiguous")
        {
            var found = await FindPostedAsync(claim, issue, github, cancellationToken);
            if (found is not null)
            {
                await RecordAsync(claim, issue, found, services, cancellationToken);
                return;
            }
        }

        var result = claim.Kind switch
        {
            "comment" => await github.CommentAsync(issue, IssueMarkdown.WithMarker(claim.Body!, claim.Id), cancellationToken),
            "close" => await github.CloseAsync(issue, claim.Reason == "not_planned" ? CloseReason.NotPlanned : CloseReason.Completed, cancellationToken),
            "reopen" => await github.ReopenAsync(issue, cancellationToken),
            _ => throw new InvalidOperationException($"github_outbox entry {claim.Id} has an unknown kind."),
        };
        await RecordAsync(claim, issue, result, services, cancellationToken);
    }

    // After an ambiguous POST: Ok with the comment's id when ShipLean's own comment carries the marker; null when it isn't there
    // (post again); Retry when the list or the login can't be read now (the entry stays ambiguous); Failed when GitHub refuses.
    private async Task<GitHubWriteResult?> FindPostedAsync(
        OutboxClaim claim, IssueRef issue, IGitHubIssues github, CancellationToken cancellationToken)
    {
        string self;
        try
        {
            self = await identity.SelfLoginAsync(cancellationToken);
        }
        catch (InvalidOperationException noLogin)
        {
            // GitHubIdentity throws when there is no token or GitHub won't say the login; without it no comment can be trusted.
            logger.LogWarning(noLogin, "GitHub outbox entry {EntryId} for {Issue}: ShipLean's GitHub login is unknown", claim.Id, issue);
            return new GitHubWriteResult.Ambiguous("noSelfLogin");
        }

        var marker = IssueMarkdown.OutboxMarker(claim.Id);
        return await github.ListRecentCommentsAsync(issue, claim.CreatedAt - ListMargin, cancellationToken) switch
        {
            GitHubCommentList.Listed listed => listed.Comments
                .Where(c => string.Equals(c.AuthorLogin, self, StringComparison.OrdinalIgnoreCase) && c.Body.Contains(marker, StringComparison.Ordinal))
                .Select(c => new GitHubWriteResult.Ok(c.Id))
                .FirstOrDefault(),
            GitHubCommentList.Retry retry => new GitHubWriteResult.Ambiguous(retry.Error),
            GitHubCommentList.Failed failed => new GitHubWriteResult.Failed(failed.Error),
            _ => throw new InvalidOperationException("Unknown comment list result."),
        };
    }

    private async Task RecordAsync(OutboxClaim claim, IssueRef issue, GitHubWriteResult result, IServiceProvider services, CancellationToken cancellationToken)
    {
        switch (result)
        {
            case GitHubWriteResult.Ok ok:
                Stale(await OutboxClaims.MarkSentAsync(dataSource, claim, ok.CommentId, clock.GetUtcNow(), cancellationToken), claim);
                return;
            case GitHubWriteResult.Failed failed:
                await FailAsync(claim, issue, "failed", failed.Error, services, cancellationToken);
                return;
            case GitHubWriteResult.Retry retry:
                await BackOffAsync(claim, issue, "retry", retry.Error, services, cancellationToken);
                return;
            case GitHubWriteResult.Ambiguous ambiguous:
                await BackOffAsync(claim, issue, "ambiguous", ambiguous.Error, services, cancellationToken);
                return;
            default:
                throw new InvalidOperationException("Unknown GitHub write result.");
        }
    }

    private async Task BackOffAsync(OutboxClaim claim, IssueRef issue, string lastResult, string error, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (RetryAfter(claim.Attempts) is not { } wait)
        {
            await FailAsync(claim, issue, lastResult, error, services, cancellationToken);
            return;
        }

        logger.LogWarning(
            "GitHub outbox entry {EntryId} ({Kind}, try {Attempt}) for {Issue} was not done: {Result} {Error}. Trying again in {Wait}.",
            claim.Id, claim.Kind, claim.Attempts, issue, lastResult, error, wait);
        Stale(await OutboxClaims.BackOffAsync(dataSource, claim, lastResult, error, clock.GetUtcNow() + wait, cancellationToken), claim);
    }

    // A comment on an issue closed on GitHub: nothing went wrong, so nothing is held and no one is told.
    private async Task FailQuietlyAsync(OutboxClaim claim, IssueRef issue, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        Stale(await OutboxClaims.FailAsync(transaction, claim, "failed", ClosedError, clock.GetUtcNow(), cancellationToken), claim);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("GitHub outbox entry {EntryId} for {Issue} was not sent: the issue is closed on GitHub", claim.Id, issue);
    }

    // Fails the entry, holds a close queued next, and adds one note on the item, all in one transaction; the note's
    // CommentsChanged is published after commit.
    private async Task FailAsync(OutboxClaim claim, IssueRef issue, string lastResult, string error, IServiceProvider services, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        CommentsChanged? changed = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            if (!await OutboxClaims.FailAsync(transaction, claim, lastResult, error, now, cancellationToken))
            {
                Stale(false, claim);
                return;
            }

            var held = await OutboxClaims.HoldNextCloseAsync(transaction, claim.GitHubIssueId, HeldError, now, cancellationToken);
            var target = await OutboxClaims.NoteTargetAsync(transaction, claim.GitHubIssueId, cancellationToken);
            if (target?.AuthorId is { } authorId)
            {
                await services.GetRequiredService<ICommentStore>().AddAsAgentAsync(
                    target.WorkItemId, new CommentCreate(Note(claim.Kind, issue, error, held)), NoteAgentName, authorId, transaction, cancellationToken);
                changed = new CommentsChanged(target.ProjectId, target.WorkItemId);
            }
            else
            {
                logger.LogWarning("GitHub outbox entry {EntryId} for {Issue}: no run-as member or item creator to write the failure note as", claim.Id, issue);
            }

            await transaction.CommitAsync(cancellationToken);
            logger.LogWarning(
                "GitHub outbox entry {EntryId} ({Kind}, try {Attempt}) for {Issue} failed for good: {Error}.{Held}",
                claim.Id, claim.Kind, claim.Attempts, issue, error, held ? " The close queued after it is held." : string.Empty);
        }

        if (changed is not null)
        {
            await services.GetRequiredService<IProjectEvents>().PublishAsync(changed);
        }
    }

    private static string Note(string kind, IssueRef issue, string error, bool held)
    {
        var what = kind switch
        {
            "comment" => "post an update to",
            "close" => "close",
            _ => "reopen",
        };
        var heldLine = held ? " The close queued after it was not sent either." : string.Empty;
        return $"ShipLean could not {what} GitHub issue {issue} ({error}).{heldLine} A maintainer should check the issue on GitHub.";
    }

    // A claim that lost its lease to another worker writes nothing: the other worker owns the entry now.
    private void Stale(bool written, OutboxClaim claim)
    {
        if (!written)
        {
            logger.LogWarning("GitHub outbox entry {EntryId}: the lease ran out before the result was recorded; another worker has it", claim.Id);
        }
    }
}
