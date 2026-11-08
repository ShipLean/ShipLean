using System.Data.Common;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Inbox;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// IIssueAcceptance over the stores (D19). An LGTM acts only while the issue is open on GitHub and its phase is awaitingReply or
// alreadyShipped, on the latest proposal, and only when it came after that proposal was posted: its GitHub comment id is greater
// than the github_comment_id of the sent outbox entry proposal:<session> (or a proposal:<session>:reopen[:<n>] copy). GitHub comment ids only
// grow, so no clock is needed; an LGTM for an earlier proposal, or one given before this one was posted, is only mirrored. It
// writes nothing but the durable intent: an accept job keyed by the proposal session, enqueued in one transaction with the
// compare-and-set of the phase to accepting (GitHub never redelivers, so the job is what finishes the acceptance; AcceptJobWorker
// runs it, and only it moves the phase on). A second LGTM, or the same one twice, finds the phase accepting and adds nothing. While a
// round is writing a proposal the reply is "a new proposal is being written"; a needsInfo proposal gets "the questions are still
// open" and nothing changes. After acceptance, on a closed issue, or for a repository no project takes now, nothing happens.
public sealed class DbIssueAcceptance(
    NpgsqlDataSource dataSource,
    IIssueIntakeSettings settings,
    IGitHubIssueStore issues,
    IIssueJobs jobs,
    IGitHubOutbox outbox,
    IIssueReplies replies,
    IWorkItemStore workItems,
    TimeProvider clock) : IIssueAcceptance
{
    private static readonly IssuePhase[] Acceptable = [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped];

    public async Task<IssueOutcome> AcceptAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        if (await issues.FindAsync(comment.Issue, null, cancellationToken) is not { OpenOnGitHub: true } record
            || (await settings.ForRepositoryAsync(comment.Issue, cancellationToken))?.ProjectId != record.ProjectId)
        {
            return IssueOutcome.Ignored;
        }

        if (record.Phase == IssuePhase.Proposing)
        {
            return await ReplyAsync(record, AcceptanceTexts.BusyKey(comment.CommentId), AcceptanceTexts.Busy, cancellationToken);
        }

        if (!Acceptable.Contains(record.Phase) || record.ProposalSessionId is not { } sessionId)
        {
            return IssueOutcome.Ignored;
        }

        if (!await PostedBeforeAsync(sessionId, comment.CommentId, cancellationToken))
        {
            return await replies.MirrorAsync(comment, cancellationToken);
        }

        if (AcceptanceReads.TryReadProposal(record.Proposal, out var proposal, out _) && proposal.Verdict == ProposalVerdict.NeedsInfo)
        {
            return await ReplyAsync(record, AcceptanceTexts.QuestionsKey(comment.CommentId), AcceptanceTexts.QuestionsOpen, cancellationToken);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // The compare-and-set locks the row: a round or hold that moves the phase waits for this commit (and then finds accepting),
        // or wins first and this LGTM changes nothing. The transaction rolls back unless it commits below.
        // A close that committed after the first read is seen here, under the row lock, and the LGTM changes nothing.
        if (!await issues.SetPhaseAsync(record.Id, [record.Phase], IssuePhase.Accepting, transaction, cancellationToken)
            || await issues.FindByIdAsync(record.Id, transaction, cancellationToken) is not { OpenOnGitHub: true } locked
            || locked.ProposalSessionId != sessionId)
        {
            return IssueOutcome.Ignored;
        }

        var payload = JsonSerializer.Serialize(new AcceptJobPayload(comment.CommentId, sessionId, record.WorkItemId));
        if (!await jobs.EnqueueAsync(IssueJobKind.Accept, AcceptanceTexts.JobKey(sessionId), record.Id, record.ProjectId, payload, transaction, cancellationToken)
            && !await RearmAsync(AcceptanceTexts.JobKey(sessionId), payload, transaction, cancellationToken))
        {
            return IssueOutcome.Ignored;
        }

        await transaction.CommitAsync(cancellationToken);
        return IssueOutcome.Processed((await workItems.GetAsync(record.WorkItemId, cancellationToken)).Key);
    }

    // A maintainer's retry: this session's accept job already ran and let the LGTM go (it failed for good, or was released), so it
    // is made due again from its first attempt with this LGTM's payload. False when it is still pending (nothing more to do).
    private async Task<bool> RearmAsync(string key, string payload, DbTransaction transaction, CancellationToken cancellationToken)
        => await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE issue_job SET status = 'pending', attempts = 0, next_attempt_at = @now, payload = @payload::jsonb, last_error = NULL,
                                 done_at = NULL, leased_until = NULL, lease_token = NULL
            WHERE key = @key AND status IN ('failed', 'done')
            """,
            new { key, payload, now = clock.GetUtcNow() },
            transaction,
            cancellationToken: cancellationToken)) == 1;

    // True when the LGTM's comment came after the proposal of this session was posted on GitHub.
    private async Task<bool> PostedBeforeAsync(Guid sessionId, long commentId, CancellationToken cancellationToken)
    {
        var key = ProposalJobWorker.DedupeKey(sessionId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var posted = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT max(github_comment_id) FROM github_outbox WHERE (dedupe_key = @key OR dedupe_key LIKE @reopen) AND sent_at IS NOT NULL",
            new { key, reopen = AcceptanceTexts.ReopenedProposalKey(sessionId) + "%" },
            cancellationToken: cancellationToken));
        return posted is { } id && commentId > id;
    }

    // A fixed reply (no agent text) through the outbox, once per LGTM comment.
    private async Task<IssueOutcome> ReplyAsync(GitHubIssueRecord record, string dedupeKey, string body, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await outbox.EnqueueAsync(OutboxEntry.Comment(record.Id, dedupeKey, body), transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return IssueOutcome.Processed((await workItems.GetAsync(record.WorkItemId, cancellationToken)).Key);
    }
}
