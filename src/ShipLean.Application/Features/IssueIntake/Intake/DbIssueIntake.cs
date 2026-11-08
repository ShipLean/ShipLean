using System.Data.Common;
using System.Text;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.IssueIntake.Intake;

// IIssueIntake over the stores. A new issue becomes a Triage item, its github_issue record and a round request in one
// transaction (the work item write's own), so a crash leaves nothing half-made and a retry starts over (D18). Every step that
// must happen once claims an effect key in that transaction; a replayed or concurrent delivery finds the claim.
public sealed class DbIssueIntake(
    NpgsqlDataSource dataSource,
    IIssueIntakeSettings settings,
    IGitHubIssueStore issues,
    IIssueEffects effects,
    IIssueProposalSessions sessions,
    IWorkItemStore workItems,
    ICommentStore comments,
    IGitHubOutbox outbox,
    IProjectEvents events) : IIssueIntake
{
    private const string ShipLeanAgent = "ShipLean";
    private const int MaxTitle = 255;

    public async Task<IssueOutcome> OpenedAsync(IssueEvent issue, CancellationToken cancellationToken)
    {
        if (await settings.ForRepositoryAsync(issue.Issue, cancellationToken) is not { } project)
        {
            return IssueOutcome.Ignored;
        }

        var newIssue = new NewIssue(issue.Issue, issue.Url, issue.Title, issue.Body, issue.AuthorLogin, issue.Labels);
        return await TakeNewAsync(project, newIssue, force: false, cancellationToken);
    }

    public async Task<IssueOutcome> ChangedAsync(IssueEvent issue, CancellationToken cancellationToken)
    {
        if (await issues.FindAsync(issue.Issue, null, cancellationToken) is not { } record)
        {
            return IssueOutcome.Ignored;
        }

        var runAs = (await settings.ForRepositoryAsync(issue.Issue, cancellationToken))?.RunAsUserId;
        var key = $"{issue.Action.ToString().ToLowerInvariant()}:{record.Id}:{issue.UpdatedAt.ToUnixTimeMilliseconds()}";
        var itemKey = (await workItems.GetAsync(record.WorkItemId, cancellationToken)).Key;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await effects.TryClaimAsync(key, record.Id, null, transaction, cancellationToken))
        {
            return IssueOutcome.Processed(itemKey);
        }

        // Order comes from GitHub's updated_at, in one UPDATE inside this transaction: a stale event (an older edit delivered
        // late, a close that lost to a newer reopen) changes nothing and writes no comment.
        string? note = null;
        var at = issue.UpdatedAt;
        switch (issue.Action)
        {
            case IssueAction.Edited when await issues.UpdateTextAsync(record.Id, issue.Title, issue.Body, at, transaction, cancellationToken):
                note = $"The issue was edited on GitHub. New title: {issue.Title}\n\n{Quote(issue.Body)}";
                break;
            case IssueAction.Closed when await issues.SetOpenOnGitHubAsync(record.Id, false, at, transaction, cancellationToken):
                note = "The issue was closed on GitHub.";
                break;
            case IssueAction.Reopened when await issues.SetOpenOnGitHubAsync(record.Id, true, at, transaction, cancellationToken):
                note = "The issue was reopened on GitHub.";
                await RepostProposalAsync(record.Id, transaction, cancellationToken);
                break;
            case IssueAction.Deleted when await issues.SetOpenOnGitHubAsync(record.Id, false, at, transaction, cancellationToken):
                note = "The issue was deleted on GitHub. ShipLean no longer posts to it; this item stays open.";
                break;
            case IssueAction.Transferred when await issues.SetOpenOnGitHubAsync(record.Id, false, at, transaction, cancellationToken):
                note = "The issue was transferred to another repository on GitHub. ShipLean no longer posts to it; this item stays open.";
                break;
            default:
                break;
        }

        var changed = false;
        if (note is not null && runAs is { } author)
        {
            await comments.AddAsAgentAsync(record.WorkItemId, new CommentCreate(note), ShipLeanAgent, author, transaction, cancellationToken);
            changed = true;
        }

        await transaction.CommitAsync(cancellationToken);
        if (changed)
        {
            await events.PublishAsync(new CommentsChanged(record.ProjectId, record.WorkItemId));
        }

        return IssueOutcome.Processed(itemKey);
    }

    // A proposal written while the issue was closed on GitHub was enqueued and then skipped by the deliverer (X3). On a reopen,
    // while that proposal is still the one awaiting a reply, it is enqueued again once (dedupe proposal:<sessionId>:reopen).
    // An entry still pending is left alone: the deliverer sends it now that the issue is open, so nothing posts twice.
    private async Task RepostProposalAsync(Guid issueId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var skipped = await transaction.Connection!.QuerySingleOrDefaultAsync<SkippedProposal>(new CommandDefinition(
            """
            SELECT i.proposal_session_id AS sessionId, o.body
            FROM github_issue i
            JOIN github_outbox o ON o.github_issue_id = i.id AND o.dedupe_key = 'proposal:' || i.proposal_session_id::text
            WHERE i.id = @issueId AND i.phase IN ('awaitingReply', 'alreadyShipped')
              AND o.sent_at IS NOT NULL AND o.last_result = @skippedClosed AND o.body IS NOT NULL
            """,
            new { issueId, skippedClosed = OutboxClaims.SkippedClosed },
            transaction,
            cancellationToken: cancellationToken));
        if (skipped is not null)
        {
            await outbox.EnqueueAsync(OutboxEntry.Comment(issueId, $"proposal:{skipped.SessionId}:reopen", skipped.Body), transaction, cancellationToken);
        }
    }

    private sealed record SkippedProposal(Guid SessionId, string Body);

    public async Task<IssueOutcome> TakeAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        if (await settings.ForRepositoryAsync(comment.Issue, cancellationToken) is not { } project)
        {
            return IssueOutcome.Ignored;
        }

        var record = await issues.FindAsync(comment.Issue, null, cancellationToken);
        if (record is null)
        {
            // The comment carries the issue's title and author but not its body or labels.
            var fromComment = new NewIssue(comment.Issue, comment.IssueUrl, comment.IssueTitle, string.Empty, comment.IssueAuthorLogin, []);
            return comment.IssueOpen ? await TakeNewAsync(project, fromComment, force: true, cancellationToken) : IssueOutcome.Ignored;
        }

        if (record.Phase != IssuePhase.WaitingForMaintainer)
        {
            return IssueOutcome.Ignored;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (!await issues.SetPhaseAsync(record.Id, [IssuePhase.WaitingForMaintainer], IssuePhase.Proposing, transaction, cancellationToken))
        {
            return IssueOutcome.Ignored;
        }

        var round = await sessions.RequestRoundAsync(record.Id, force: true, transaction, cancellationToken);
        if (round.Outcome != RoundRequestOutcome.Requested)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IssueOutcome.Ignored;
        }

        await transaction.CommitAsync(cancellationToken);
        if (round.Changed is { } roundChanged)
        {
            await events.PublishAsync(roundChanged);
        }

        return IssueOutcome.Processed((await workItems.GetAsync(record.WorkItemId, cancellationToken)).Key);
    }

    private async Task<IssueOutcome> TakeNewAsync(IssueIntakeProject project, NewIssue issue, bool force, CancellationToken cancellationToken)
    {
        if (await issues.FindAsync(issue.Ref, null, cancellationToken) is { } existing)
        {
            return IssueOutcome.Processed((await workItems.GetAsync(existing.WorkItemId, cancellationToken)).Key);
        }

        var type = issue.Labels.Any(l => string.Equals(l, "bug", StringComparison.OrdinalIgnoreCase)) ? WorkItemTypeId.Bug : WorkItemTypeId.Story;
        var triage = await TriageStateAsync(project.ProjectId, type, cancellationToken);
        var changes = new WorkItemChanges
        {
            State = new FieldSet<Guid>(triage),
            Description = new FieldSet<string>($"Reported on GitHub by @{issue.AuthorLogin}\n\n{Quote(issue.Body)}"),
            AddLinks = [new LinkValue(LinkType.Url, null, null, issue.Url, $"GitHub {issue.Ref}")],
        };
        var create = new WorkItemCreate(project.ProjectId, type, Truncate(issue.Title), null, null, changes);
        try
        {
            CommentsChanged? roundChanged = null;
            var options = WriteOptions.FromWebhook($"GitHub {issue.Ref}", async (transaction, token) => roundChanged = await RecordAsync(project, issue, force, transaction, token));
            var key = (await workItems.CreateAsync(create, options, cancellationToken)).Written!.Key;
            if (roundChanged is not null)
            {
                await events.PublishAsync(roundChanged);
            }

            return IssueOutcome.Processed(key);
        }
        catch (IssueTakenException)
        {
            // A concurrent delivery created the record first; this create rolled back.
            var taken = await issues.FindAsync(issue.Ref, null, cancellationToken);
            return IssueOutcome.Processed((await workItems.GetAsync(taken!.WorkItemId, cancellationToken)).Key);
        }
    }

    // Runs inside the work item create's transaction, which holds the project's item lock, so the newest item is ours.
    // Returns the round request's CommentsChanged (a hold notice), for the caller to publish once the create has committed.
    private async Task<CommentsChanged?> RecordAsync(IssueIntakeProject project, NewIssue issue, bool force, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var itemId = await transaction.Connection!.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT id FROM work_item WHERE project_id = @ProjectId ORDER BY number DESC LIMIT 1", new { project.ProjectId }, transaction, cancellationToken: cancellationToken));
        var record = await issues.InsertAsync(
            issue.Ref, project.ProjectId, itemId, issue.AuthorLogin, issue.Title, IssueEvent.Cap(issue.Body), IssuePhase.Proposing, transaction, cancellationToken);
        if (record.WorkItemId != itemId || !await effects.TryClaimAsync($"open:{record.Id}", record.Id, itemId, transaction, cancellationToken))
        {
            throw new IssueTakenException();
        }

        var round = await sessions.RequestRoundAsync(record.Id, force, transaction, cancellationToken);
        if (round.Outcome != RoundRequestOutcome.Requested)
        {
            await issues.SetPhaseAsync(record.Id, [IssuePhase.Proposing], IssuePhase.WaitingForMaintainer, transaction, cancellationToken);
        }

        return round.Changed;
    }

    private async Task<Guid> TriageStateAsync(Guid projectId, WorkItemTypeId type, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var typeName = type == WorkItemTypeId.Bug ? "bug" : "story";
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM state WHERE project_id = @projectId AND type = @typeName AND is_intake AND archived_at IS NULL ORDER BY sort_order LIMIT 1",
            new { projectId, typeName }, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Project {projectId} takes GitHub issues but has no Triage state for {typeName} items.");
    }

    private static string Truncate(string title)
    {
        var text = title.Trim();
        if (text.Length <= MaxTitle)
        {
            return text;
        }

        var cut = char.IsHighSurrogate(text[MaxTitle - 1]) ? MaxTitle - 1 : MaxTitle;
        return text[..cut];
    }

    private static string Quote(string text)
    {
        var builder = new StringBuilder();
        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
        {
            builder.Append("> ").Append(line).Append('\n');
        }

        return builder.ToString().TrimEnd();
    }

    private sealed record NewIssue(IssueRef Ref, string Url, string Title, string Body, string AuthorLogin, IReadOnlyList<string> Labels);

}

// Thrown inside the create's hook so the half-made item rolls back when another delivery took the issue first.
public sealed class IssueTakenException : Exception
{
}
