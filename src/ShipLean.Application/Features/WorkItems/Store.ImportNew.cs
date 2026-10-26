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
    // Inserts an item the source system numbered: its key number is kept and the project's sequence moves past it. History is
    // the source's changelog, one revision each; an item with none gets one revision that creates it.
    private async Task<ImportResult> ImportNewAsync(WriteScope scope, Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken)
    {
        var projectKey = await scope.Connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "UPDATE project SET next_item_number = GREATEST(next_item_number, @number + 1) WHERE id = @projectId RETURNING key",
            new { projectId, number = item.KeyNumber }, scope.Transaction, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("Project not found.");
        var taken = await scope.Connection.QuerySingleAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM work_item WHERE project_id = @projectId AND number = @number)",
            new { projectId, number = item.KeyNumber }, scope.Transaction, cancellationToken: cancellationToken));
        if (taken)
        {
            throw ApiException.Conflict($"Key number {item.KeyNumber} belongs to another work item.");
        }

        var id = Guid.NewGuid();
        var key = $"{projectKey}-{item.KeyNumber}";
        var seed = new ProposedChange(
            id, projectId, item.Type, item.Title, item.Description, item.AcceptanceCriteria, item.ReproSteps, item.Severity, item.StateId, item.AssigneeId,
            item.SprintId, item.Priority, WorkItemPoints.Normalise(item.Points), item.ParentId, item.Tags, null, false, null, null,
            await WorkItemOrdering.KeyAsync(scope, OrderScope.Backlog(projectId), id, Slot.Last, null, cancellationToken));
        var (columnId, laneId, columnOrder) = await WorkItemPlacement.InitialAsync(scope, projectId, id, item.Type, item.StateId, cancellationToken);
        var proposed = seed with { ColumnId = columnId, LaneId = laneId, ColumnOrder = columnOrder };
        var parentType = await WorkItemChecks.ValidateAsync(scope, proposed, cancellationToken);
        var parameters = new DynamicParameters(ColumnValues(proposed, parentType));
        parameters.Add("projectId", projectId);
        parameters.Add("number", item.KeyNumber);
        parameters.Add("key", key);
        parameters.Add("type", WorkItemTypes.Db(item.Type));
        parameters.Add("system", item.OriginalSystem.System);
        parameters.Add("externalId", item.OriginalSystem.Id);
        parameters.Add("url", item.OriginalSystem.Url);
        parameters.Add("createdBy", item.CreatedBy);
        parameters.Add("createdAt", item.CreatedAt);
        parameters.Add("changedBy", item.ChangedBy);
        parameters.Add("changedAt", item.ChangedAt);
        parameters.Add("stateChangedAt", item.StateChangedAt);
        // Inserted at revision 1 and raised once the history is written, so the count always matches the revision rows.
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_item (id, project_id, number, key, type, title, description, acceptance_criteria, repro_steps, severity, state_id,
                                   assigned_to, sprint_id, priority, story_points, backlog_order, parent_id, parent_type, tags, board_column_id,
                                   board_column_done, swimlane_id, column_order, original_system, original_system_id, original_url, revision,
                                   created_by, created_at, changed_by, changed_at, state_changed_at)
            VALUES (@id, @projectId, @number, @key, @type, @title, @description, @criteria::jsonb, @repro, @severity, @stateId,
                    @assignee, @sprint, @priority, @points, @backlogOrder, @parentId, @parentType, @tags, @columnId,
                    @columnDone, @laneId, @columnOrder, @system, @externalId, @url, 1,
                    @createdBy, @createdAt, @changedBy, @changedAt, @stateChangedAt)
            """,
            parameters, scope.Transaction, cancellationToken: cancellationToken));
        var links = await WorkItemLinks.ApplyAsync(scope, id, projectId, item.CreatedBy, [], new WorkItemChanges { AddLinks = item.Links }, cancellationToken);
        var revisions = item.Revisions.Count > 0
            ? item.Revisions
            : [new ImportedRevision(item.CreatedBy, item.CreatedAt, await DiffAsync(scope, null, proposed, links, cancellationToken))];
        var revision = 0;
        foreach (var imported in revisions)
        {
            await InsertImportedRevisionAsync(scope, id, ++revision, importJobId, item.OriginalSystem.System, imported, cancellationToken);
        }

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE work_item SET revision = @revision WHERE id = @id", new { id, revision }, scope.Transaction, cancellationToken: cancellationToken));
        scope.Publish([new WorkItemCreated(projectId, id, key, item.Type, item.ParentId)]);
        return new ImportResult(id, key, revision, true);
    }

    private static Task InsertImportedRevisionAsync(
        WriteScope scope, Guid id, int revision, Guid importJobId, string system, ImportedRevision imported, CancellationToken cancellationToken)
        => scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO revision (work_item_id, rev, changed_by, changed_at, original_system, import_job_id, changes)
            VALUES (@id, @revision, @changedBy, @changedAt, @system, @importJobId, @changes::jsonb)
            """,
            new
            {
                id,
                revision,
                changedBy = imported.ChangedBy,
                changedAt = imported.ChangedAt,
                system,
                importJobId,
                changes = JsonSerializer.Serialize(imported.Changes, PersistedJson.Options),
            },
            scope.Transaction,
            cancellationToken: cancellationToken));
}
