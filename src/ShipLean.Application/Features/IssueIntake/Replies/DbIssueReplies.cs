using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Replies;

// IIssueReplies over the stores. Every comment on a taken issue is mirrored onto its item, once per GitHub URL. A reply from
// the author or a maintainer also asks for the next round when the conversation is waiting for one (D11); the phase
// compare-and-set, the round request and the effect claim share one transaction, so two deliveries of one reply start one round.
public sealed class DbIssueReplies(
    NpgsqlDataSource dataSource,
    IIssueIntakeSettings settings,
    IGitHubIssueStore issues,
    IMirroredComments mirrored,
    IIssueEffects effects,
    IIssueProposalSessions sessions,
    IWorkItemStore workItems,
    IProjectEvents events) : IIssueReplies
{
    // The reason IIssueProposalSessions documents for a request that wrote nothing.
    private const string IntakeOff = "intakeOff";

    // Only these phases wait for a round; any other phase (proposing, accepting, accepted, closed) means mirror only.
    private static readonly IssuePhase[] WaitingPhases = [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped, IssuePhase.WaitingForMaintainer];

    public async Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        return await MirrorCoreAsync(comment, cancellationToken) is { } taken
            ? IssueOutcome.Processed(taken.ItemKey)
            : IssueOutcome.Ignored;
    }

    public async Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        if (await MirrorCoreAsync(comment, cancellationToken) is not { } taken)
        {
            return IssueOutcome.Ignored;
        }

        if (WaitingPhases.Contains(taken.Record.Phase))
        {
            await RequestRoundAsync(taken.Record, comment, cancellationToken);
        }

        return IssueOutcome.Processed(taken.ItemKey);
    }

    private async Task<Taken?> MirrorCoreAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        if (await settings.ForRepositoryAsync(comment.Issue, cancellationToken) is not { RunAsUserId: { } runAs }
            || await issues.FindAsync(comment.Issue, null, cancellationToken) is not { } record)
        {
            return null;
        }

        var add = new MirroredComment(record.Id, comment.CommentId, comment.CommentUrl, comment.AuthorLogin, IssueEvent.Cap(comment.Body), comment.CreatedAt, runAs);
        MirroredCommentAdd result;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            result = await mirrored.AddAsync(add, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        if (result.Changed is { } changed)
        {
            await events.PublishAsync(changed);
        }

        return new Taken(record, (await workItems.GetAsync(record.WorkItemId, cancellationToken)).Key);
    }

    private async Task RequestRoundAsync(GitHubIssueRecord record, IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await effects.TryClaimAsync($"reply:{record.Id}:{comment.CommentId}", record.Id, null, transaction, cancellationToken)
            || !await issues.SetPhaseAsync(record.Id, WaitingPhases, IssuePhase.Proposing, transaction, cancellationToken))
        {
            return;
        }

        var round = await sessions.RequestRoundAsync(record.Id, force: false, transaction, cancellationToken);
        if (round is { Outcome: RoundRequestOutcome.Held, Reason: IntakeOff })
        {
            // The project no longer takes this repository: nothing is written, not even the phase.
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await transaction.CommitAsync(cancellationToken);
        if (round.Changed is { } changed)
        {
            await events.PublishAsync(changed);
        }
    }

    private sealed record Taken(GitHubIssueRecord Record, string ItemKey);
}
