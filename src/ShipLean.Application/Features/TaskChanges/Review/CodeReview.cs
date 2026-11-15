using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.TaskChanges.Review;

// ICodeReview: Approve and Request changes. The state move goes through the work item store, the history line through IRevisionNotes.
public sealed class CodeReview(
    NpgsqlDataSource dataSource,
    IWorkItemStore store,
    IStateCatalog catalog,
    ICommentStore comments,
    IRevisionNotes notes,
    IProjectEvents events,
    TimeProvider clock) : ICodeReview
{
    public async Task<ReviewOutcome> ApproveAsync(Guid workItemId, ApproveRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var shas = (await connection.QueryAsync<LiveCommit>(new CommandDefinition(
            """
            SELECT id, sha FROM work_item_commit WHERE work_item_id = @workItemId AND archived_at IS NULL ORDER BY position, id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        if (shas.Count == 0 || shas[^1].Sha != shas.FirstOrDefault(c => c.Id == request.HeadCommitId)?.Sha)
        {
            throw ApiException.Conflict("A newer commit arrived while you were reviewing. Reload the changes and look again.");
        }

        var item = await store.GetAsync(workItemId, cancellationToken);
        var states = await catalog.ListAsync(item.ProjectId, cancellationToken);
        var target = First(states, item.Type, StateCategory.Completed);
        await MoveAsync(item, target, null, cancellationToken);
        await notes.AddAsync(workItemId, FieldIds.Review, $"Approved {string.Join(", ", shas.Select(c => c.Sha[..7]))}", WriteOptions.User, cancellationToken);
        await PostNoteAsync(workItemId, request.Note, cancellationToken);
        return new ReviewOutcome(target.Name, await SentBackRows.CountOpenAsync(connection, workItemId, cancellationToken));
    }

    public async Task<ReviewOutcome> RequestChangesAsync(Guid workItemId, RequestChangesRequest request, CancellationToken cancellationToken = default)
    {
        var item = await store.GetAsync(workItemId, cancellationToken);
        var states = await catalog.ListAsync(item.ProjectId, cancellationToken);
        var target = First(states, item.Type, request.KeepAssignee ? StateCategory.InProgress : StateCategory.Proposed);
        await MoveAsync(item, target, request.KeepAssignee ? null : new FieldSet<Guid?>(null), cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var marked = await SentBackRows.MarkAsync(connection, workItemId, clock.GetUtcNow(), cancellationToken);
        if (marked > 0)
        {
            await events.PublishAsync(new CodeCommentsChanged(item.ProjectId, workItemId));
        }

        await notes.AddAsync(workItemId, FieldIds.Review, $"Changes requested · {marked} {(marked == 1 ? "comment" : "comments")}", WriteOptions.User, cancellationToken);
        await PostNoteAsync(workItemId, request.Note, cancellationToken);
        return new ReviewOutcome(target.Name, marked);
    }

    // The first state of the category for the type, in state order (the same rule as the agent tools).
    private static StateDto First(IReadOnlyList<StateDto> states, WorkItemTypeId type, StateCategory category)
        => states.Where(s => s.Type == type && s.Category == category).OrderBy(s => s.Order).FirstOrDefault()
           ?? throw ApiException.Validation($"A {type} in this project has no {category} state.");

    private async Task MoveAsync(WorkItemSnapshot item, StateDto target, FieldSet<Guid?>? assignee, CancellationToken cancellationToken)
    {
        var changes = new WorkItemChanges
        {
            State = target.Id == item.StateId ? null : new FieldSet<Guid>(target.Id),
            Assignee = assignee is not null && item.AssigneeId is not null ? assignee : null,
        };
        if (changes is { State: null, Assignee: null })
        {
            return;
        }

        var outcome = await store.PatchAsync(item.Id, new WorkItemPatch(item.Revision, changes), WriteOptions.User, cancellationToken);
        if (outcome.Conflict is not null)
        {
            throw ApiException.Conflict($"{item.Key} changed while you were reviewing. Reload and look again.");
        }
    }

    private async Task PostNoteAsync(Guid workItemId, string? note, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(note))
        {
            await comments.AddAsync(workItemId, new CommentCreate(note.Trim()), cancellationToken);
        }
    }

    private sealed record LiveCommit(Guid Id, string Sha);
}
