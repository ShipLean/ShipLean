using Dapper;
using ShipLean.Application.WorkItems;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// What a write must satisfy before it reaches the tables, checked on the change the rules returned. Each failure is the
// ApiException the contract names; the schema's own constraints stay a second line of defence.
internal static class WorkItemChecks
{
    // Returns the parent's type (null without a parent), which the row stores beside parent_id.
    public static async Task<WorkItemTypeId?> ValidateAsync(WriteScope scope, ProposedChange change, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(change.Title))
        {
            throw ApiException.Validation("A work item needs a title.");
        }

        if (change.Priority is < 1 or > 4 || change.Severity is < 1 or > 4 || change.Points is < 0 or > 9999.99m)
        {
            throw ApiException.Validation("Priority and severity run from 1 to 4, and points from 0 to 9999.99.");
        }

        FeatureFieldRules.Check(change.Type, change.Goal, change.StatusLine, change.StatusVerifiedAt);
        await CheckStateAsync(scope, change, cancellationToken);
        if (change.AssigneeId is { } assignee && !await ExistsAsync(scope, "SELECT EXISTS (SELECT 1 FROM app_user WHERE id = @id)", assignee, change.ProjectId, cancellationToken))
        {
            throw ApiException.NotFound("User not found.");
        }

        if (change.SprintId is { } sprint
            && !await ExistsAsync(scope, "SELECT EXISTS (SELECT 1 FROM sprint WHERE id = @id AND project_id = @projectId AND archived_at IS NULL)", sprint, change.ProjectId, cancellationToken))
        {
            throw ApiException.NotFound("Sprint not found.");
        }

        return await CheckParentAsync(scope, change, cancellationToken);
    }

    private static async Task CheckStateAsync(WriteScope scope, ProposedChange change, CancellationToken cancellationToken)
    {
        var type = await scope.Connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT type FROM state WHERE id = @id AND project_id = @projectId AND archived_at IS NULL",
            new { id = change.StateId, projectId = change.ProjectId }, scope.Transaction, cancellationToken: cancellationToken));
        if (type != WorkItemTypes.Db(change.Type))
        {
            throw ApiException.Validation("That state doesn't belong to this type of work item.");
        }
    }

    private static async Task<WorkItemTypeId?> CheckParentAsync(WriteScope scope, ProposedChange change, CancellationToken cancellationToken)
    {
        if (change.ParentId is not { } parentId)
        {
            return null;
        }

        var parent = await scope.Connection.QuerySingleOrDefaultAsync<ParentRow>(new CommandDefinition(
            "SELECT project_id AS projectId, type FROM work_item WHERE id = @id", new { id = parentId }, scope.Transaction, cancellationToken: cancellationToken));
        if (parent is null || parent.ProjectId != change.ProjectId)
        {
            throw ApiException.NotFound("Parent not found.");
        }

        var parentType = WorkItemTypes.Parse(parent.Type);
        if (parentId == change.WorkItemId || !WorkItemTypes.IsAllowedParent(change.Type, parentType))
        {
            throw ApiException.Validation($"A {change.Type} can't have a {parentType} as its parent.");
        }

        if (change.Type == WorkItemTypeId.Feature && await IsAncestorAsync(scope, change.WorkItemId, parentId, cancellationToken))
        {
            throw ApiException.Validation("A Feature can't be placed under one of its own sub-features.");
        }

        return parentType;
    }

    // Whether candidate is above (or is) the item's new parent, which would make the Feature its own ancestor.
    private static Task<bool> IsAncestorAsync(WriteScope scope, Guid candidate, Guid parentId, CancellationToken cancellationToken)
        => scope.Connection.QuerySingleAsync<bool>(new CommandDefinition(
            """
            WITH RECURSIVE above AS (
                SELECT id, parent_id FROM work_item WHERE id = @parentId
                UNION
                SELECT w.id, w.parent_id FROM work_item w JOIN above a ON w.id = a.parent_id
            )
            SELECT EXISTS (SELECT 1 FROM above WHERE id = @candidate)
            """,
            new { parentId, candidate }, scope.Transaction, cancellationToken: cancellationToken));

    private static Task<bool> ExistsAsync(WriteScope scope, string sql, Guid id, Guid projectId, CancellationToken cancellationToken)
        => scope.Connection.QuerySingleAsync<bool>(new CommandDefinition(sql, new { id, projectId }, scope.Transaction, cancellationToken: cancellationToken));

    private sealed record ParentRow(Guid ProjectId, string Type);
}
