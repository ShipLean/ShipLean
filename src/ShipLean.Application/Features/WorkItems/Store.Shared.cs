using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Markdown;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems;

// The only writer of work_item, its revision rows and its link rows. Every write runs in one transaction: the rules
// run inside it, the InTransaction hook runs last, and the events go out only after it has committed. This file holds
// what Create, Patch and PatchMany share; the stores' reads are in Store.Reads.cs.
public sealed partial class WorkItemStore(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    IWorkItemRules rules,
    IProjectEvents events,
    MarkdownRenderer renderer,
    TimeProvider time,
    ICurrentAgent currentAgent) : IWorkItemStore
{
    private readonly WorkItemReads _reads = new(dataSource, renderer, time);

    // Opens the transaction and runs body in it. The body says whether to commit (false = a conflict: nothing was written).
    private async Task<T> WriteAsync<T>(WriteOptions options, Func<WriteScope, Task<(T Result, bool Commit)>> body, CancellationToken cancellationToken)
    {
        T result;
        IReadOnlyList<ProjectEvent> published;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var scope = new WriteScope(connection, transaction, time.GetUtcNow(), options.Origin, options.Webhook, options.Via, options.Bulk);
            (result, var commit) = await body(scope);
            if (!commit)
            {
                return result;
            }

            if (options.InTransaction is { } hook)
            {
                await hook(transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            published = scope.Events;
        }

        foreach (var projectEvent in published)
        {
            await events.PublishAsync(projectEvent);
        }

        return result;
    }

    // The outcome for a stale expected revision: the 409 body, nothing written.
    private async Task<WriteOutcome> StaleAsync(WriteScope scope, WorkItemRow row, int expectedRevision, CancellationToken cancellationToken)
        => new(null, await _reads.ConflictAsync(scope.Connection, scope.Transaction, row.Id, expectedRevision, cancellationToken));

    private static async Task<IReadOnlyDictionary<Guid, WorkItemRow>> LockAsync(WriteScope scope, Guid[] ids, CancellationToken cancellationToken)
    {
        await WorkItemProjectLock.TakeForItemsAsync(scope, ids, cancellationToken);
        var rows = await WorkItemQueries.RowsAsync(scope.Connection, scope.Transaction, $"{WorkItemRow.Select} WHERE w.id = ANY(@ids) ORDER BY w.id FOR UPDATE OF w", new { ids }, cancellationToken);
        return rows.Count == ids.Distinct().Count() ? rows.ToDictionary(r => r.Id) : throw ApiException.NotFound("Work item not found.");
    }

    // Runs the rules (unless runRules is false: Remove), checks the result, writes the item (inserting it when before is null),
    // its links and, when a history field changed, a revision. An item whose only difference is its position is updated
    // without a revision. When the rules moved the card to another ordering scope (column, Done half, Taskboard cell) and
    // left the order alone, the order is recomputed in the final scope: before beforeItemId when that card is in it, else last.
    private async Task<WriteResult> PersistAsync(
        WriteScope scope, WorkItemSnapshot? before, NewItem? created, ProposedChange proposed, WorkItemChanges? linkChanges, bool runRules, Guid? beforeItemId, CancellationToken cancellationToken)
    {
        linkChanges = WorkItemProposals.Clean(linkChanges);
        var outcome = runRules ? await rules.ApplyAsync(before, proposed with { Bulk = scope.Bulk }, ActorOf(scope), scope.Transaction, cancellationToken) : new RuleOutcome(proposed, []);
        var amended = outcome.AmendedChange;
        var finalScope = OrderScope.Of(amended);
        if (NeedsNewKey(before, proposed, amended, finalScope))
        {
            amended = amended with
            {
                ColumnOrder = finalScope is null ? null : await WorkItemOrdering.KeyAsync(scope, finalScope, amended.WorkItemId, Slot.BeforeOrLast, beforeItemId, cancellationToken),
            };
        }

        var parentType = await WorkItemChecks.ValidateAsync(scope, amended, cancellationToken);
        if (created is not null)
        {
            await InsertAsync(scope, created, amended, parentType, cancellationToken);
        }

        var linksAfter = await WorkItemLinks.ApplyAsync(scope, amended.WorkItemId, amended.ProjectId, ActorOf(scope), before?.Links ?? [], linkChanges, cancellationToken);
        var changes = await DiffAsync(scope, before, amended, linksAfter, cancellationToken);
        var revision = created is not null ? 1 : before!.Revision;
        if (changes.Count > 0 && created is null)
        {
            revision++;
        }

        if (before is not null && (changes.Count > 0 || PositionDiffers(before, amended)))
        {
            await UpdateAsync(scope, before, amended, parentType, revision, changes.Count > 0, cancellationToken);
        }

        if (changes.Count > 0)
        {
            await InsertRevisionAsync(scope, amended.WorkItemId, revision, changes, cancellationToken);
        }

        var key = created?.Key ?? before!.Key;
        scope.Publish(EventsFor(created is not null, amended, key, revision, changes, outcome.Notices));
        return new WriteResult(amended.WorkItemId, key, revision, changes, outcome.Notices);
    }

    // The order is recomputed when the card ends up in another ordering scope than the one it was proposed in (the rules moved it)
    // or than the one it is stored in (a patch of a task's state or parent moved it), and nobody gave it a key in the new scope.
    // A card with no stored column order has not been placed yet (a new task), so it stays unplaced.
    private static bool NeedsNewKey(WorkItemSnapshot? before, ProposedChange proposed, ProposedChange amended, OrderScope? finalScope)
    {
        if (amended.ColumnOrder != proposed.ColumnOrder)
        {
            return false;
        }

        if (finalScope != OrderScope.Of(proposed))
        {
            return true;
        }

        return before?.ColumnOrder is not null && proposed.ColumnOrder == before.ColumnOrder && finalScope != OrderScope.Of(WorkItemProposals.From(before));
    }

    private static IEnumerable<ProjectEvent> EventsFor(
        bool isNew, ProposedChange item, string key, int revision, IReadOnlyList<FieldChange> changes, IReadOnlyList<string> notices)
    {
        if (isNew)
        {
            yield return new WorkItemCreated(item.ProjectId, item.WorkItemId, key, item.Type, item.ParentId);
        }
        else if (changes.Count > 0)
        {
            yield return new WorkItemUpdated(item.ProjectId, item.WorkItemId, revision, [.. changes.Select(c => c.Field)], notices.Contains("pulled"));
        }
    }

    private static bool PositionDiffers(WorkItemSnapshot before, ProposedChange after)
        => before.ColumnId != after.ColumnId || before.ColumnDone != after.ColumnDone || before.LaneId != after.LaneId
           || before.ColumnOrder != after.ColumnOrder || before.BacklogOrder != after.BacklogOrder;

    private static async Task<IReadOnlyList<FieldChange>> DiffAsync(
        WriteScope scope, WorkItemSnapshot? before, ProposedChange after, IReadOnlyList<LinkSnapshot> linksAfter, CancellationToken cancellationToken)
    {
        var beforeView = before is null ? WorkItemFieldView.Blank : WorkItemProposals.ViewOf(WorkItemProposals.From(before), before.Links);
        var afterView = WorkItemProposals.ViewOf(after, linksAfter);
        var states = await NamesAsync(scope, "SELECT id, name FROM state WHERE id = ANY(@ids)", [beforeView.State, afterView.State], cancellationToken);
        var users = await NamesAsync(scope, "SELECT id, name FROM app_user WHERE id = ANY(@ids)", [beforeView.Assignee, afterView.Assignee], cancellationToken);
        var sprints = await NamesAsync(scope, "SELECT id, name FROM sprint WHERE id = ANY(@ids)", [beforeView.Sprint, afterView.Sprint], cancellationToken);
        var items = await NamesAsync(scope, "SELECT id, title AS name FROM work_item WHERE id = ANY(@ids)", [beforeView.Parent, afterView.Parent], cancellationToken);
        var labels = new WorkItemFieldLabels(id => states.GetValueOrDefault(id), id => users.GetValueOrDefault(id), id => sprints.GetValueOrDefault(id), id => items.GetValueOrDefault(id), DisplayDate.Format);
        return WorkItemFieldDiff.Between(beforeView, afterView, labels);
    }

    private static async Task<Dictionary<Guid, string>> NamesAsync(WriteScope scope, string sql, IEnumerable<Guid?> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.OfType<Guid>().Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return [];
        }

        var rows = await scope.Connection.QueryAsync<NameRow>(new CommandDefinition(sql, new { ids = distinct }, scope.Transaction, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    private static object ColumnValues(ProposedChange c, WorkItemTypeId? parentType) => new
    {
        id = c.WorkItemId,
        title = c.Title,
        description = c.Description,
        criteria = JsonSerializer.Serialize(c.AcceptanceCriteria, PersistedJson.Options),
        repro = c.ReproSteps,
        severity = c.Severity,
        stateId = c.StateId,
        assignee = c.AssigneeId,
        sprint = c.SprintId,
        priority = c.Priority,
        points = c.Points,
        backlogOrder = c.BacklogOrder,
        parentId = c.ParentId,
        parentType = parentType is { } type ? WorkItemTypes.Db(type) : null,
        tags = c.Tags.ToArray(),
        columnId = c.ColumnId,
        columnDone = c.ColumnDone,
        laneId = c.LaneId,
        columnOrder = c.ColumnOrder,
        goal = WorkItemFieldView.NullIfEmpty(c.Goal),
        statusLine = WorkItemFieldView.NullIfEmpty(c.StatusLine),
        statusVerifiedAt = c.StatusVerifiedAt,
    };

    private async Task InsertAsync(WriteScope scope, NewItem created, ProposedChange c, WorkItemTypeId? parentType, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters(ColumnValues(c, parentType));
        parameters.Add("projectId", c.ProjectId);
        parameters.Add("number", created.Number);
        parameters.Add("key", created.Key);
        parameters.Add("type", WorkItemTypes.Db(c.Type));
        parameters.Add("actor", ActorOf(scope));
        parameters.Add("at", scope.At);
        var stamp = StatusStamp(scope, c);
        parameters.Add("verifiedBy", stamp?.By);
        parameters.Add("verifiedAgent", stamp?.Agent);
        parameters.Add("verifiedVia", stamp?.Via);
        parameters.Add("slug", created.Slug);
        parameters.Add("startedOn", created.StartedOn);
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_item (id, project_id, number, key, type, title, description, acceptance_criteria, repro_steps, severity, state_id,
                                   assigned_to, sprint_id, priority, story_points, backlog_order, parent_id, parent_type, tags, board_column_id,
                                   board_column_done, swimlane_id, column_order, revision, created_by, created_at, changed_by, changed_at, state_changed_at,
                                   goal, status_line, status_verified_at, status_verified_by, status_verified_agent, status_verified_via, slug, started_on)
            VALUES (@id, @projectId, @number, @key, @type, @title, @description, @criteria::jsonb, @repro, @severity, @stateId,
                    @assignee, @sprint, @priority, @points, @backlogOrder, @parentId, @parentType, @tags, @columnId,
                    @columnDone, @laneId, @columnOrder, 1, @actor, @at, @actor, @at, @at,
                    @goal, @statusLine, @statusVerifiedAt, @verifiedBy, @verifiedAgent, @verifiedVia, @slug, @startedOn)
            """,
            parameters, scope.Transaction, cancellationToken: cancellationToken));
    }

    private async Task UpdateAsync(
        WriteScope scope, WorkItemSnapshot before, ProposedChange c, WorkItemTypeId? parentType, int revision, bool written, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters(ColumnValues(c, parentType));
        parameters.Add("revision", revision);
        parameters.Add("changedBy", written ? ActorOf(scope) : before.ChangedBy);
        parameters.Add("changedAt", written ? scope.At : before.ChangedAt);
        parameters.Add("stateChangedAt", c.StateId == before.StateId ? before.StateChangedAt : scope.At);
        var stamp = c.StatusVerifiedAt == before.StatusVerifiedAt
            ? new StatusStampValues(before.StatusVerifiedBy, before.StatusVerifiedAgent, before.StatusVerifiedVia)
            : StatusStamp(scope, c);
        parameters.Add("verifiedBy", stamp?.By);
        parameters.Add("verifiedAgent", stamp?.Agent);
        parameters.Add("verifiedVia", stamp?.Via);
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE work_item SET title = @title, description = @description, acceptance_criteria = @criteria::jsonb, repro_steps = @repro,
                   severity = @severity, state_id = @stateId, assigned_to = @assignee, sprint_id = @sprint, priority = @priority,
                   story_points = @points, backlog_order = @backlogOrder, parent_id = @parentId, parent_type = @parentType, tags = @tags,
                   board_column_id = @columnId, board_column_done = @columnDone, swimlane_id = @laneId, column_order = @columnOrder,
                   revision = @revision, changed_by = @changedBy, changed_at = @changedAt, state_changed_at = @stateChangedAt,
                   goal = @goal, status_line = @statusLine, status_verified_at = @statusVerifiedAt, status_verified_by = @verifiedBy,
                   status_verified_agent = @verifiedAgent, status_verified_via = @verifiedVia
            WHERE id = @id
            """,
            parameters, scope.Transaction, cancellationToken: cancellationToken));
    }

    private async Task InsertRevisionAsync(WriteScope scope, Guid id, int revision, IReadOnlyList<FieldChange> changes, CancellationToken cancellationToken)
        => await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO revision (work_item_id, rev, changed_by, changed_at, original_system, agent_name, webhook, bulk, changes)
            VALUES (@id, @revision, @actor, @at, @originalSystem, @agentName, @webhook, @bulk, @changes::jsonb)
            """,
            new
            {
                id,
                revision,
                actor = ActorOf(scope),
                at = scope.At,
                originalSystem = scope.Origin == RevisionOrigin.Jira ? "jira" : null,
                agentName = AgentStamp.Of(currentAgent),
                webhook = scope.Webhook,
                bulk = scope.Bulk,
                changes = JsonSerializer.Serialize(changes, PersistedJson.Options),
            },
            scope.Transaction,
            cancellationToken: cancellationToken));

    // Who the write acts as: the request's user, or nobody for a webhook write (a webhook request has no user).
    private Guid? ActorOf(WriteScope scope) => scope.Origin == RevisionOrigin.Webhook ? null : currentUser.Id;

    // Who verified the status, set when the write stamps it: the acting user, the agent acting for them and the MCP tool, if any.
    private StatusStampValues? StatusStamp(WriteScope scope, ProposedChange c)
        => c.StatusVerifiedAt is null ? null : new StatusStampValues(ActorOf(scope), AgentStamp.Of(currentAgent), scope.Via);

    private sealed record StatusStampValues(Guid? By, string? Agent, string? Via);

    // The key and number a create reserved from the project's sequence, and the Feature's slug and start day it was asked for.
    private sealed record NewItem(string Key, int Number, string? Slug = null, DateOnly? StartedOn = null);

    private sealed record NameRow(Guid Id, string Name);
}
