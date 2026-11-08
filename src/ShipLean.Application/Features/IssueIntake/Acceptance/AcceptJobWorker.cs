using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// Runs one `accept` job at a time (D6, D18, D19). The LGTM moved the phase to accepting, and only this job moves it on. Every step
// is idempotent and a retry re-runs them all, so a crash anywhere finishes on the next claim with the same items. Every step
// commits under the github_issue row lock (SELECT ... FOR UPDATE), rechecking that the issue is still accepting this session and
// that this worker still holds the job's lease; otherwise the step rolls back and the run stops (AcceptanceLostException):
// 1. create the item (the create claims accept-create:<session> with the new id inside its own transaction; a retry reads the id
//    back) or patch the Triage item (the patch claims accept-patch:<session> in its transaction, so a retry never patches again
//    over a maintainer's edit);
// 2. repoint the issue at the item that carries the work;
// 3. move the Triage item to Removed with a note, unless it carries the work or is already Removed;
// 4. and 5. in one transaction: the GitHub reply (deduped in the outbox), then the phase (accepting to accepted, or closed for
//    alreadyShipped) as a compare-and-set, with the job's completion. The phase is last, so progress never sees a half-done
//    acceptance. An issue closed on GitHub meanwhile still finishes, with no reply.
// The reply passes PublicTextGuard first: an unsafe reply is never posted and holds the issue for a maintainer instead. ShipLean
// notes on the items are by the project's run-as member as the agent "ShipLean". Anything thrown fails the job, which retries.
public sealed class AcceptJobWorker(
    IIssueJobs jobs,
    IGitHubIssueStore issues,
    IGitHubOutbox outbox,
    IIssueEffects effects,
    AcceptanceReads reads,
    RoundHolds holds,
    IServiceScopeFactory scopes,
    NpgsqlDataSource dataSource,
    IProjectEvents events,
    ILogger<AcceptJobWorker> logger)
{
    private static readonly IssuePhase[] Accepting = [IssuePhase.Accepting];

    // False when no accept job was due.
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        var job = await jobs.ClaimNextAsync(IssueJobKind.Accept, cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await RunAsync(job, cancellationToken);
        }
        catch (AcceptanceLostException lost)
        {
            // Only this job moves the phase on, so the acceptance is finished or another worker has the job: nothing to retry.
            logger.LogInformation(lost, "Accept job {JobId} stopped", job.Id);
            await jobs.CompleteAsync(job.Id, job.LeaseToken, null, CancellationToken.None);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Accept job {JobId} for github_issue {IssueId} failed", job.Id, job.GitHubIssueId);
            if (job.Attempts >= IIssueJobs.MaxAttempts)
            {
                await FailForGoodAsync(job, failed.Message, CancellationToken.None);
            }
            else
            {
                await jobs.FailAsync(job.Id, job.LeaseToken, failed.Message, null, CancellationToken.None);
            }
        }

        return true;
    }

    private async Task RunAsync(IssueJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<AcceptJobPayload>(job.Payload) ?? throw new InvalidOperationException($"Accept job {job.Id} has no payload.");
        var issue = await issues.FindByIdAsync(job.GitHubIssueId, null, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {job.GitHubIssueId}.");
        if (issue.Phase != IssuePhase.Accepting || issue.ProposalSessionId != payload.SessionId)
        {
            // Accepted already, or a job left from before the accepting phase: nothing to do.
            logger.LogInformation("Accept job {JobId}: {Issue} is not accepting this proposal ({Phase})", job.Id, issue.Issue, issue.Phase);
            await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
            return;
        }

        var project = await TakingProject.FindAsync(scopes, issue, cancellationToken);
        if (!AcceptanceReads.TryReadProposal(issue.Proposal, out var proposal, out var failure) || project is null)
        {
            // No longer taken, or a stored proposal that doesn't read: the LGTM is let go and the issue waits for a reply again.
            logger.LogWarning("Accept job {JobId}: {Issue} is not accepted ({Failure})", job.Id, issue.Issue, failure ?? "the repository is no longer taken");
            await ReleaseAsync(job, issue.Id, proposal is null ? IssuePhase.AwaitingReply : PhaseBefore(proposal), cancellationToken);
            return;
        }

        var runAs = project.RunAsUserId ?? throw new InvalidOperationException($"Project {project.ProjectKey} takes issues with no run-as member.");
        await using var scope = scopes.CreateAsyncScope();
        var run = new Run(job, payload, issue, project, proposal, runAs, scope.ServiceProvider.GetRequiredService<IWorkItemStore>(),
            scope.ServiceProvider.GetRequiredService<ICommentStore>(), await reads.GuardAsync(project.ProjectId, cancellationToken));
        var triage = await run.Items.GetAsync(payload.TriageItemId, cancellationToken);
        var duplicate = proposal.Verdict == ProposalVerdict.Duplicate
            ? await reads.FindItemAsync(project.ProjectId, project.ProjectKey, proposal.Existing.FirstOrDefault()?.Key, cancellationToken)
            : null;
        if (duplicate?.Id == triage.Id)
        {
            duplicate = null;
        }

        switch (proposal.Verdict)
        {
            case ProposalVerdict.AlreadyShipped:
                await ShippedAsync(run, triage, cancellationToken);
                break;
            case ProposalVerdict.NeedsInfo:
                // IIssueAcceptance answers a needsInfo LGTM itself and enqueues no job; a job left from before changes nothing.
                await ReleaseAsync(job, issue.Id, IssuePhase.AwaitingReply, cancellationToken);
                break;
            case ProposalVerdict.Duplicate when duplicate is not null:
                await DuplicateAsync(run, triage, duplicate, cancellationToken);
                break;
            default:
                await NewAsync(run, triage, cancellationToken);
                break;
        }
    }

    private async Task NewAsync(Run run, WorkItemSnapshot triage, CancellationToken cancellationToken)
    {
        var type = AcceptanceReads.TypeOf(run.Proposal.Type);
        if (!await PreCheckAsync(run, AcceptanceTexts.Accepted(triage.Key, run.Proposal.Title), cancellationToken))
        {
            return;
        }

        var accepted = await reads.AcceptedStateAsync(run.Project.ProjectId, type, cancellationToken);
        var parent = await reads.ParentAsync(run.Project.ProjectId, run.Project.ProjectKey, type, run.Proposal.ParentKey, cancellationToken);
        var changes = new WorkItemChanges
        {
            Title = new FieldSet<string>(run.Proposal.Title),
            Description = new FieldSet<string>(run.Proposal.Description ?? string.Empty),
            AcceptanceCriteria = new FieldSet<IReadOnlyList<ChecklistItem>>([.. run.Proposal.AcceptanceCriteria.Select(c => new ChecklistItem(c, false))]),
            State = new FieldSet<Guid>(accepted),
            Parent = parent is null ? null : new FieldSet<Guid?>(parent),
        };

        WorkItemSnapshot target;
        if (type == triage.Type)
        {
            await PatchTriageAsync(run, triage, changes, cancellationToken);
            target = triage;
        }
        else
        {
            target = await run.Items.GetAsync(await CreateAsync(run, triage, type, parent, changes, cancellationToken), cancellationToken);
            await events.PublishAsync(new CommentsChanged(run.Project.ProjectId, target.Id));
            await RepointAsync(run, target.Id, cancellationToken);
            await RemoveTriageAsync(run, triage, AcceptanceTexts.Became(target.Key), cancellationToken);
        }

        await FinishAsync(run, triage, AcceptanceTexts.Accepted(target.Key, run.Proposal.Title), null, IssuePhase.Accepted, cancellationToken);
    }

    private async Task DuplicateAsync(Run run, WorkItemSnapshot triage, ProjectItem existing, CancellationToken cancellationToken)
    {
        var reply = AcceptanceTexts.Duplicate(existing.Key, existing.Title);
        if (!await PreCheckAsync(run, reply, cancellationToken))
        {
            return;
        }

        await RepointAsync(run, existing.Id, cancellationToken);
        await RemoveTriageAsync(run, triage, AcceptanceTexts.DuplicateOf(existing.Key), cancellationToken);
        await FinishAsync(run, triage, reply, null, IssuePhase.Accepted, cancellationToken);
    }

    private async Task ShippedAsync(Run run, WorkItemSnapshot triage, CancellationToken cancellationToken)
    {
        if (!await PreCheckAsync(run, AcceptanceTexts.Shipped, cancellationToken))
        {
            return;
        }

        await RemoveTriageAsync(run, triage, AcceptanceTexts.AlreadyShippedNote, cancellationToken);
        await FinishAsync(run, triage, AcceptanceTexts.Shipped, CloseReason.Completed, IssuePhase.Closed, cancellationToken);
    }

    // Patches the Triage item once: the patch claims accept-patch:<session> in its own transaction, so a retry after it (even
    // after a maintainer edited the item since) skips it.
    private async Task PatchTriageAsync(Run run, WorkItemSnapshot triage, WorkItemChanges changes, CancellationToken cancellationToken)
    {
        var key = AcceptanceTexts.PatchKey(run.Payload.SessionId);
        if (await effects.FindAsync(key, null, cancellationToken) is not null)
        {
            return;
        }

        var options = Options(run.Issue) with { BeforePatch = (transaction, token) => ClaimPatchAsync(run, key, triage.Id, transaction, token) };
        try
        {
            if (!await PatchAsync(run.Items, triage.Id, triage.Revision, changes, options, cancellationToken))
            {
                throw new InvalidOperationException($"The Triage item {triage.Key} kept changing; the acceptance retries.");
            }
        }
        catch (AcceptStepTakenException)
        {
            // A concurrent run of the same job patched it first; this patch rolled back.
        }
    }

    private async Task ClaimPatchAsync(Run run, string key, Guid itemId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await LockAsync(run, transaction, cancellationToken);
        if (!await effects.TryClaimAsync(key, run.Issue.Id, itemId, transaction, cancellationToken))
        {
            throw new AcceptStepTakenException();
        }
    }

    private async Task RepointAsync(Run run, Guid itemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockAsync(run, transaction, cancellationToken);
        await issues.RepointAsync(run.Issue.Id, itemId, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Creates the item of another type once: the claim of accept-create:<session> commits with the create, so a retry finds it.
    private async Task<Guid> CreateAsync(
        Run run, WorkItemSnapshot triage, WorkItemTypeId type, Guid? parent, WorkItemChanges changes, CancellationToken cancellationToken)
    {
        var key = AcceptanceTexts.CreateKey(run.Payload.SessionId);
        if (await effects.FindAsync(key, null, cancellationToken) is { } made)
        {
            return made;
        }

        var create = new WorkItemCreate(run.Project.ProjectId, type, run.Proposal.Title, parent, null, changes with
        {
            Title = null,
            Parent = null,
            AddLinks =
            [
                new LinkValue(LinkType.Url, null, null, $"https://github.com/{run.Issue.Issue.Repository}/issues/{run.Issue.Issue.Number}", $"GitHub {run.Issue.Issue}"),
                new LinkValue(LinkType.Related, triage.Id, triage.Key, null, null),
            ],
        });
        try
        {
            var options = WriteOptions.FromWebhook($"GitHub {run.Issue.Issue}", (transaction, token) => ClaimCreateAsync(run, triage, key, transaction, token));
            return (await run.Items.CreateAsync(create, options, cancellationToken)).Written!.Id;
        }
        catch (AcceptStepTakenException)
        {
            // A concurrent claim of the same job created it first; this create rolled back.
            return await effects.FindAsync(key, null, cancellationToken) ?? throw new InvalidOperationException($"{key} was claimed with no item.");
        }
    }

    // Inside the create's transaction, which holds the project's item lock, so the newest item is the one just created. The new
    // item's "Came from" note commits with it (item links carry no title, so the related link alone can't say it).
    private async Task ClaimCreateAsync(Run run, WorkItemSnapshot triage, string key, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await LockAsync(run, transaction, cancellationToken);
        var itemId = await transaction.Connection!.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT id FROM work_item WHERE project_id = @ProjectId ORDER BY number DESC LIMIT 1", new { run.Project.ProjectId }, transaction, cancellationToken: cancellationToken));
        if (!await effects.TryClaimAsync(key, run.Issue.Id, itemId, transaction, cancellationToken))
        {
            throw new AcceptStepTakenException();
        }

        await run.Comments.AddAsAgentAsync(itemId, new CommentCreate(AcceptanceTexts.CameFrom(triage.Key)), RoundHolds.AgentName, run.RunAs, transaction, cancellationToken);
    }

    // The note on the Triage item once per acceptance, then Removed unless it is already in a Removed state.
    private async Task RemoveTriageAsync(Run run, WorkItemSnapshot triage, string note, CancellationToken cancellationToken)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            await LockAsync(run, transaction, cancellationToken);
            if (await effects.TryClaimAsync(AcceptanceTexts.NoteKey(run.Payload.SessionId, triage.Id), run.Issue.Id, null, transaction, cancellationToken))
            {
                await run.Comments.AddAsAgentAsync(triage.Id, new CommentCreate(note), RoundHolds.AgentName, run.RunAs, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var current = await run.Items.GetAsync(triage.Id, cancellationToken);
        if (await reads.IsRemovedAsync(current.StateId, cancellationToken))
        {
            return;
        }

        var removed = await reads.RemovedStateAsync(run.Project.ProjectId, current.Type, cancellationToken);
        var changes = new WorkItemChanges { State = new FieldSet<Guid>(removed) };
        var options = Options(run.Issue) with { BeforePatch = (transaction, token) => LockAsync(run, transaction, token) };
        if (!await PatchAsync(run.Items, current.Id, current.Revision, changes, options, cancellationToken))
        {
            throw new InvalidOperationException($"The Triage item {current.Key} kept changing; the acceptance retries.");
        }
    }

    // Checks the reply before anything is written. Unsafe: the phase goes back from accepting, the issue is held for a maintainer
    // (with why, on the Triage item) and the job completes, in one transaction, and nothing is accepted.
    private async Task<bool> PreCheckAsync(Run run, string reply, CancellationToken cancellationToken)
    {
        var guarded = run.Guard.Clean(reply, run.Issue.AuthorLogin);
        if (guarded.Safe)
        {
            return true;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var reason = guarded.Removed.Count == 0 ? "unknown" : guarded.Removed[^1];
        await LockAsync(run, transaction, cancellationToken);
        await issues.SetPhaseAsync(run.Issue.Id, Accepting, PhaseBefore(run.Proposal), transaction, cancellationToken);
        var noted = await holds.HoldAsync(run.Issue, AcceptanceTexts.UnsafeReason, AcceptanceTexts.Unsafe(reason), run.RunAs, transaction, cancellationToken);
        if (!await jobs.CompleteAsync(run.Job.Id, run.Job.LeaseToken, transaction, cancellationToken))
        {
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        if (noted)
        {
            await events.PublishAsync(new CommentsChanged(run.Issue.ProjectId, run.Issue.WorkItemId));
        }

        return false;
    }

    // Steps 4 and 5: the reply (and the close) only when the phase moves, the phase last, and the job's completion, together. A
    // stale lease (another worker has the job now) rolls all of it back.
    private async Task FinishAsync(Run run, WorkItemSnapshot triage, string reply, CloseReason? close, IssuePhase phase, CancellationToken cancellationToken)
    {
        var guarded = run.Guard.Clean(reply, run.Issue.AuthorLogin);
        if (!guarded.Safe)
        {
            // Passed the check before the steps with the Triage key in place of the final one; the job retries and posts nothing.
            throw new InvalidOperationException("The acceptance reply did not pass the public text check.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var open = await LockAsync(run, transaction, cancellationToken);
        if (await issues.SetPhaseAsync(run.Issue.Id, Accepting, phase, transaction, cancellationToken) && open)
        {
            await outbox.EnqueueAsync(OutboxEntry.Comment(run.Issue.Id, AcceptanceTexts.ReplyKey(run.Payload.SessionId), guarded.Text), transaction, cancellationToken);
            if (close is { } reason)
            {
                await outbox.EnqueueAsync(OutboxEntry.Close(run.Issue.Id, AcceptanceTexts.CloseKey(run.Payload.SessionId), reason), transaction, cancellationToken);
            }
        }

        if (!await jobs.CompleteAsync(run.Job.Id, run.Job.LeaseToken, transaction, cancellationToken))
        {
            return;
        }

        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new CommentsChanged(run.Issue.ProjectId, triage.Id));
    }

    // The guard every step runs first in its transaction: locks the github_issue row, so a round, a reply, a hold or a close waits
    // for the step, and checks that the issue is still accepting this job's session under this worker's lease. True when the
    // issue is open on GitHub.
    private static async Task<bool> LockAsync(Run run, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var connection = transaction.Connection!;
        var row = await connection.QuerySingleAsync<LockedIssue>(new CommandDefinition(
            "SELECT phase AS Phase, proposal_session_id AS SessionId, open_on_github AS OpenOnGitHub FROM github_issue WHERE id = @Id FOR UPDATE",
            new { run.Issue.Id },
            transaction,
            cancellationToken: cancellationToken));
        var lease = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT lease_token FROM issue_job WHERE id = @Id AND status = 'pending' FOR UPDATE",
            new { run.Job.Id },
            transaction,
            cancellationToken: cancellationToken));
        if (IssueWire.ParsePhase(row.Phase) != IssuePhase.Accepting || row.SessionId != run.Payload.SessionId)
        {
            throw new AcceptanceLostException($"{run.Issue.Issue} is no longer accepting session {run.Payload.SessionId} ({row.Phase}).");
        }

        if (lease != run.Job.LeaseToken)
        {
            throw new AcceptanceLostException($"Another worker holds accept job {run.Job.Id} now.");
        }

        return row.OpenOnGitHub;
    }

    // The last try failed: in one transaction the job fails for good, accepting goes back to the phase it came from (awaitingReply,
    // or alreadyShipped for an alreadyShipped proposal), and a ShipLean note by the run-as member says a maintainer can retry. The
    // note's CommentsChanged is published after commit. Only while the issue is still accepting this job's session.
    private async Task FailForGoodAsync(IssueJob job, string error, CancellationToken cancellationToken)
    {
        var sessionId = JsonSerializer.Deserialize<AcceptJobPayload>(job.Payload)?.SessionId;
        CommentsChanged? changed = null;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            if (!await jobs.FailAsync(job.Id, job.LeaseToken, error, transaction, cancellationToken))
            {
                return;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT 1 FROM github_issue WHERE id = @GitHubIssueId FOR UPDATE", new { job.GitHubIssueId }, transaction, cancellationToken: cancellationToken));
            var issue = await issues.FindByIdAsync(job.GitHubIssueId, transaction, cancellationToken);
            if (issue is { Phase: IssuePhase.Accepting } && issue.ProposalSessionId == sessionId)
            {
                var back = AcceptanceReads.TryReadProposal(issue.Proposal, out var proposal, out _) ? PhaseBefore(proposal) : IssuePhase.AwaitingReply;
                await issues.SetPhaseAsync(issue.Id, Accepting, back, transaction, cancellationToken);
                if ((await TakingProject.FindAsync(scopes, issue, cancellationToken))?.RunAsUserId is { } runAs)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ICommentStore>().AddAsAgentAsync(
                        issue.WorkItemId, new CommentCreate(AcceptanceTexts.Failed), RoundHolds.AgentName, runAs, transaction, cancellationToken);
                    changed = new CommentsChanged(issue.ProjectId, issue.WorkItemId);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (changed is not null)
        {
            await events.PublishAsync(changed);
        }
    }

    // The LGTM is let go: accepting goes back to the phase it came from, with the job's completion.
    private async Task ReleaseAsync(IssueJob job, Guid issueId, IssuePhase back, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await issues.SetPhaseAsync(issueId, Accepting, back, transaction, cancellationToken);
        if (await jobs.CompleteAsync(job.Id, job.LeaseToken, transaction, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static IssuePhase PhaseBefore(IssueProposal proposal)
        => proposal.Verdict == ProposalVerdict.AlreadyShipped ? IssuePhase.AlreadyShipped : IssuePhase.AwaitingReply;

    // A patch retried with the item's current revision when someone saved it meanwhile. False when every attempt was stale.
    private static async Task<bool> PatchAsync(IWorkItemStore store, Guid id, int revision, WorkItemChanges changes, WriteOptions options, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var outcome = await store.PatchAsync(id, new WorkItemPatch(revision, changes), options, cancellationToken);
            if (outcome.Written is not null)
            {
                return true;
            }

            revision = outcome.Conflict!.Current.Revision;
        }

        return false;
    }

    private static WriteOptions Options(GitHubIssueRecord issue) => WriteOptions.FromWebhook($"GitHub {issue.Issue}");

    private sealed record LockedIssue(string Phase, Guid? SessionId, bool OpenOnGitHub);

    private sealed record Run(
        IssueJob Job,
        AcceptJobPayload Payload,
        GitHubIssueRecord Issue,
        IssueIntakeProject Project,
        IssueProposal Proposal,
        Guid RunAs,
        IWorkItemStore Items,
        ICommentStore Comments,
        PublicTextGuard Guard);
}

