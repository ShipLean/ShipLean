using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Replies;

// IIssueReplies over the stores. Every comment on a taken issue is mirrored onto its item, once per GitHub URL. A reply from
// the author or a maintainer also asks for a round (D11) while the conversation can take one; the round service decides what
// that means (a new round, a hold, or a rerun of the round in flight). The mirror, the claim of reply:<issueId>:<commentId> and
// the round request are one transaction, so a crash between them leaves nothing and the retry does all three, and two deliveries
// of one reply ask once. Lock order, as for every issue writer: the project's round lock first, then the comment's html_url
// lock and the rows. The phase is never written here: RequestRoundAsync owns it.
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

    private const string RoundSavepoint = "reply_round";

    // A reply asks for a round in these phases (proposing: the round service reruns the round in flight). Any other phase
    // (accepting: an LGTM's accept job runs; accepted, closed) means mirror only.
    private static readonly IssuePhase[] RoundPhases = [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped, IssuePhase.WaitingForMaintainer, IssuePhase.Proposing];

    public Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        return HandleAsync(comment, reply: false, cancellationToken);
    }

    public Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        return HandleAsync(comment, reply: true, cancellationToken);
    }

    // Intake is checked first: a repository its record's project no longer takes is Ignored, with nothing written.
    private async Task<IssueOutcome> HandleAsync(IssueCommentEvent comment, bool reply, CancellationToken cancellationToken)
    {
        if (await settings.ForRepositoryAsync(comment.Issue, cancellationToken) is not { RunAsUserId: { } runAs } project
            || await issues.FindAsync(comment.Issue, null, cancellationToken) is not { } found
            || found.ProjectId != project.ProjectId)
        {
            return IssueOutcome.Ignored;
        }

        MirroredCommentAdd mirror;
        RoundRequest? round = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await sessions.LockProjectAsync(found.ProjectId, transaction, cancellationToken);
            var record = await issues.FindByIdAsync(found.Id, transaction, cancellationToken) ?? found;
            mirror = await mirrored.AddAsync(
                new MirroredComment(record.Id, comment.CommentId, comment.CommentUrl, comment.AuthorLogin, IssueEvent.Cap(comment.Body), comment.CreatedAt, runAs),
                transaction,
                cancellationToken);
            if (reply && RoundPhases.Contains(record.Phase))
            {
                round = await RequestRoundAsync(record, comment, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (mirror.Changed is { } mirrorChanged)
        {
            await events.PublishAsync(mirrorChanged);
        }

        if (round?.Changed is { } roundChanged)
        {
            await events.PublishAsync(roundChanged);
        }

        return IssueOutcome.Processed((await workItems.GetAsync(found.WorkItemId, cancellationToken)).Key);
    }

    // Null when this reply already asked (a replay) or the project stopped taking the repository since the check above; then the
    // savepoint drops the claim too, so no round write is left.
    private async Task<RoundRequest?> RequestRoundAsync(GitHubIssueRecord record, IssueCommentEvent comment, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await transaction.SaveAsync(RoundSavepoint, cancellationToken);
        if (!await effects.TryClaimAsync($"reply:{record.Id}:{comment.CommentId}", record.Id, null, transaction, cancellationToken))
        {
            return null;
        }

        var round = await sessions.RequestRoundAsync(record.Id, force: false, transaction, cancellationToken);
        if (round is { Outcome: RoundRequestOutcome.Held, Reason: IntakeOff })
        {
            await transaction.RollbackAsync(RoundSavepoint, cancellationToken);
            return null;
        }

        return round;
    }
}
