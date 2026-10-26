using System.Text.Json;
using Dapper;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    // A re-run: the source's revisions newer than the last one stored are appended as they are, then, when the item still differs
    // from the source's values in a field those revisions didn't bring to them, one more revision (by the item's last editor)
    // ends the history at the item's values. Backlog position stays as it is. Board position stays too, unless the state changed to
    // one the card's column doesn't map, or a task changed parent (its Taskboard order is per state and parent): then the card is
    // placed as a new import is, by WorkItemPlacement.InitialAsync.
    private async Task<ImportResult> ImportExistingAsync(WriteScope scope, WorkItemRow row, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken)
    {
        if (WorkItemTypes.Parse(row.Type) != item.Type)
        {
            throw ApiException.Validation("An imported work item can't change type on a re-run.");
        }

        var linksBefore = (await WorkItemQueries.LinksAsync(scope.Connection, scope.Transaction, [row.Id], cancellationToken)).GetValueOrDefault(row.Id) ?? [];
        var before = row.ToSnapshot(linksBefore);
        var proposed = WorkItemProposals.From(before) with
        {
            Title = item.Title,
            Description = item.Description,
            AcceptanceCriteria = item.AcceptanceCriteria,
            ReproSteps = item.ReproSteps,
            Severity = item.Severity,
            StateId = item.StateId,
            AssigneeId = item.AssigneeId,
            SprintId = item.SprintId,
            Priority = item.Priority,
            Points = WorkItemPoints.Normalise(item.Points),
            ParentId = item.ParentId,
            Tags = item.Tags,
        };
        if (await NeedsPlacementAsync(scope, row, item, cancellationToken))
        {
            var (columnId, laneId, columnOrder) = await WorkItemPlacement.InitialAsync(scope, row.ProjectId, row.Id, item.Type, item.StateId, cancellationToken);
            proposed = proposed with { ColumnId = columnId, ColumnDone = false, LaneId = laneId, ColumnOrder = columnOrder };
        }

        var parentType = await WorkItemChecks.ValidateAsync(scope, proposed, cancellationToken);
        var linksAfter = await WorkItemLinks.ApplyAsync(scope, row.Id, row.ProjectId, item.ChangedBy, linksBefore, new WorkItemChanges { AddLinks = item.Links }, cancellationToken);
        var lastImported = await scope.Connection.QuerySingleAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT max(changed_at) FROM revision WHERE work_item_id = @id AND original_system IS NOT NULL",
            new { id = row.Id }, scope.Transaction, cancellationToken: cancellationToken));
        var appended = item.Revisions.Where(r => lastImported is null || r.ChangedAt > lastImported).OrderBy(r => r.ChangedAt).ToList();
        var revision = row.Revision;
        foreach (var imported in appended)
        {
            await InsertImportedRevisionAsync(scope, row.Id, ++revision, importJobId, item.OriginalSystem.System, imported, cancellationToken);
        }

        // A field the appended history already brought to its final value needs no combined revision.
        var brought = new Dictionary<string, JsonElement>();
        foreach (var change in appended.SelectMany(r => r.Changes))
        {
            brought[change.Field] = change.To;
        }

        var remaining = (await DiffAsync(scope, before, proposed, linksAfter, cancellationToken))
            .Where(c => !(brought.TryGetValue(c.Field, out var to) && JsonElement.DeepEquals(to, c.To)))
            .ToList();
        var last = appended.Count > 0 ? appended[^1] : null;
        var changedBy = row.ChangedBy;
        var changedAt = row.ChangedAt;
        if (last is not null)
        {
            (changedBy, changedAt) = (last.ChangedBy, last.ChangedAt);
        }

        if (remaining.Count > 0)
        {
            await InsertImportedRevisionAsync(scope, row.Id, ++revision, importJobId, item.OriginalSystem.System, new ImportedRevision(item.ChangedBy, item.ChangedAt, remaining), cancellationToken);
            (changedBy, changedAt) = (item.ChangedBy, item.ChangedAt);
        }

        var parameters = new DynamicParameters(ColumnValues(proposed, parentType));
        parameters.Add("url", item.OriginalSystem.Url);
        parameters.Add("revision", revision);
        parameters.Add("changedBy", changedBy);
        parameters.Add("changedAt", changedAt);
        parameters.Add("stateChangedAt", item.StateChangedAt);
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE work_item SET title = @title, description = @description, acceptance_criteria = @criteria::jsonb, repro_steps = @repro,
                   severity = @severity, state_id = @stateId, assigned_to = @assignee, sprint_id = @sprint, priority = @priority,
                   story_points = @points, parent_id = @parentId, parent_type = @parentType, tags = @tags, original_url = @url,
                   board_column_id = @columnId, board_column_done = @columnDone, swimlane_id = @laneId, column_order = @columnOrder,
                   revision = @revision, changed_by = @changedBy, changed_at = @changedAt, state_changed_at = @stateChangedAt
            WHERE id = @id
            """,
            parameters, scope.Transaction, cancellationToken: cancellationToken));
        if (revision > row.Revision)
        {
            var fields = appended.SelectMany(r => r.Changes).Concat(remaining).Select(c => c.Field).Distinct().ToList();
            scope.Publish([new WorkItemUpdated(row.ProjectId, row.Id, revision, fields, false)]);
        }

        if (proposed.ColumnId != row.ColumnId || proposed.ColumnDone != row.ColumnDone || proposed.LaneId != row.LaneId || proposed.ColumnOrder != row.ColumnOrder)
        {
            scope.Publish([new WorkItemMoved(row.ProjectId, row.Id, revision, proposed.ColumnId, proposed.ColumnDone, proposed.LaneId, proposed.ColumnOrder, proposed.StateId, proposed.ParentId)]);
        }

        return new ImportResult(row.Id, row.Key, revision, false);
    }

    // A card needs placing again when its state changed to one its column doesn't map (a task has no column: any state change),
    // or when a task changed parent.
    private static async Task<bool> NeedsPlacementAsync(WriteScope scope, WorkItemRow row, ImportedWorkItem item, CancellationToken cancellationToken)
    {
        if (WorkItemTypes.BoardLevel(item.Type) is not { } level)
        {
            return item.StateId != row.StateId || item.ParentId != row.ParentId;
        }

        if (item.StateId == row.StateId)
        {
            return false;
        }

        var json = await scope.Connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM board WHERE project_id = @projectId AND level = @level FOR SHARE",
            new { projectId = row.ProjectId, level }, scope.Transaction, cancellationToken: cancellationToken));
        if (json is null || row.ColumnId is null)
        {
            return true;
        }

        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(json, PersistedJson.Options)!;
        var column = settings.Columns.FirstOrDefault(c => c.Id == row.ColumnId);
        return column is null || !column.StateMappings.Any(m => m.Type == item.Type && m.StateId == item.StateId);
    }
}
