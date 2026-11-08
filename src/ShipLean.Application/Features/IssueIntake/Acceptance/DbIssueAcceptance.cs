using System.Text.Json;
using Npgsql;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// IIssueAcceptance over the stores (D19). An LGTM acts only while the issue is open on GitHub and its phase is awaitingReply or
// alreadyShipped, on the latest proposal. It writes nothing but the durable intent: an accept job keyed by the proposal session,
// enqueued in one transaction with a compare-and-set that the phase is still right (GitHub never redelivers, so the job is what
// finishes the acceptance; AcceptJobWorker runs it). A second LGTM, or the same one twice, finds the job and adds nothing. While a
// round is writing a proposal the reply is "a new proposal is being written"; a needsInfo proposal gets "the questions are still
// open" and nothing changes. After acceptance, on a closed issue, or for a repository no project takes now, nothing happens.
public sealed class DbIssueAcceptance(
    NpgsqlDataSource dataSource,
    IIssueIntakeSettings settings,
    IGitHubIssueStore issues,
    IIssueJobs jobs,
    IGitHubOutbox outbox,
    IWorkItemStore workItems) : IIssueAcceptance
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

        if (AcceptanceReads.TryReadProposal(record.Proposal, out var proposal, out _) && proposal.Verdict == ProposalVerdict.NeedsInfo)
        {
            return await ReplyAsync(record, AcceptanceTexts.QuestionsKey(comment.CommentId), AcceptanceTexts.QuestionsOpen, cancellationToken);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // A same-value compare-and-set locks the row: a round or hold that moves the phase waits for this commit, or wins first.
        if (!await issues.SetPhaseAsync(record.Id, Acceptable, record.Phase, transaction, cancellationToken)
            || (await issues.FindByIdAsync(record.Id, transaction, cancellationToken))?.ProposalSessionId != sessionId)
        {
            return IssueOutcome.Ignored;
        }

        var payload = JsonSerializer.Serialize(new AcceptJobPayload(comment.CommentId, sessionId, record.WorkItemId));
        await jobs.EnqueueAsync(IssueJobKind.Accept, AcceptanceTexts.JobKey(sessionId), record.Id, record.ProjectId, payload, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return IssueOutcome.Processed((await workItems.GetAsync(record.WorkItemId, cancellationToken)).Key);
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
