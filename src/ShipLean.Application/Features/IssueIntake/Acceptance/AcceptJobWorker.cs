using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// Runs one `accept` job at a time (D6, D18, D19). Every step is idempotent and a retry re-runs them all, so a crash anywhere
// finishes on the next claim with the same items:
// 1. create the item (the create claims accept-create:<session> with the new id inside its own transaction; a retry reads the id
//    back) or patch the Triage item (a patch to the same values changes nothing);
// 2. repoint the issue at the item that carries the work;
// 3. move the Triage item to Removed with a note, unless it carries the work or is already Removed;
// 4. and 5. in one transaction: the GitHub reply (deduped in the outbox), then the phase (accepted, or closed for alreadyShipped)
//    as a compare-and-set, with the job's completion. The phase is last, so progress never sees a half-done acceptance.
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
    private static readonly IssuePhase[] Acceptable = [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped];

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
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Accept job {JobId} for github_issue {IssueId} failed", job.Id, job.GitHubIssueId);
            await jobs.FailAsync(job.Id, job.LeaseToken, failed.Message, null, CancellationToken.None);
        }

        return true;
    }

    private async Task RunAsync(IssueJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<AcceptJobPayload>(job.Payload) ?? throw new InvalidOperationException($"Accept job {job.Id} has no payload.");
        var issue = await issues.FindByIdAsync(job.GitHubIssueId, null, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {job.GitHubIssueId}.");
        var project = await TakingProject.FindAsync(scopes, issue, cancellationToken);
        if (!Acceptable.Contains(issue.Phase) || issue.ProposalSessionId != payload.SessionId || project is null)
        {
            // Accepted already, closed, held, superseded by a newer proposal, or no longer taken: nothing to do.
            logger.LogInformation("Accept job {JobId}: {Issue} is no longer acceptable ({Phase})", job.Id, issue.Issue, issue.Phase);
            await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
            return;
        }

        if (!AcceptanceReads.TryReadProposal(issue.Proposal, out var proposal, out var failure))
        {
            throw new InvalidOperationException($"The stored proposal of {issue.Issue} does not read: {failure}");
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
                await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
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
            if (!await LinkWrites.PatchAsync(run.Items, triage.Id, triage.Revision, changes, Options(run.Issue), cancellationToken))
            {
                throw new InvalidOperationException($"The Triage item {triage.Key} kept changing; the acceptance retries.");
            }

            target = triage;
        }
        else
        {
            target = await run.Items.GetAsync(await CreateAsync(run, triage, type, parent, changes, cancellationToken), cancellationToken);
            await issues.RepointAsync(run.Issue.Id, target.Id, null, cancellationToken);
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

        await issues.RepointAsync(run.Issue.Id, existing.Id, null, cancellationToken);
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
                new LinkValue(LinkType.Related, triage.Id, triage.Key, null, AcceptanceTexts.CameFrom(triage.Key)),
            ],
        });
        try
        {
            var options = WriteOptions.FromWebhook($"GitHub {run.Issue.Issue}", (transaction, token) => ClaimCreateAsync(run, key, transaction, token));
            return (await run.Items.CreateAsync(create, options, cancellationToken)).Written!.Id;
        }
        catch (AcceptCreateTakenException)
        {
            // A concurrent claim of the same job created it first; this create rolled back.
            return await effects.FindAsync(key, null, cancellationToken) ?? throw new InvalidOperationException($"{key} was claimed with no item.");
        }
    }

    // Inside the create's transaction, which holds the project's item lock, so the newest item is the one just created.
    private async Task ClaimCreateAsync(Run run, string key, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var itemId = await transaction.Connection!.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT id FROM work_item WHERE project_id = @ProjectId ORDER BY number DESC LIMIT 1", new { run.Project.ProjectId }, transaction, cancellationToken: cancellationToken));
        if (!await effects.TryClaimAsync(key, run.Issue.Id, itemId, transaction, cancellationToken))
        {
            throw new AcceptCreateTakenException();
        }
    }

    // The note on the Triage item once per acceptance, then Removed unless it is already in a Removed state.
    private async Task RemoveTriageAsync(Run run, WorkItemSnapshot triage, string note, CancellationToken cancellationToken)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
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
        if (!await LinkWrites.PatchAsync(run.Items, current.Id, current.Revision, changes, Options(run.Issue), cancellationToken))
        {
            throw new InvalidOperationException($"The Triage item {current.Key} kept changing; the acceptance retries.");
        }
    }

    // Checks the reply before anything is written. Unsafe: the issue is held for a maintainer (with why, on the Triage item) and
    // the job completes, in one transaction, and nothing is accepted.
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
        var open = (await issues.FindByIdAsync(run.Issue.Id, transaction, cancellationToken))?.OpenOnGitHub == true;
        if (await issues.SetPhaseAsync(run.Issue.Id, Acceptable, phase, transaction, cancellationToken) && open)
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

    private static WriteOptions Options(GitHubIssueRecord issue) => WriteOptions.FromWebhook($"GitHub {issue.Issue}");

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

// Thrown inside the create's hook so the item rolls back when a concurrent run of the same job created it first.
public sealed class AcceptCreateTakenException : Exception
{
}
