using Dapper;
using ShipLean.Application.WorkItems;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    public Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async scope =>
        {
            await WorkItemProjectLock.TakeAsync(scope, [create.ProjectId], cancellationToken);
            return (await CreateInAsync(scope, create, cancellationToken), true);
        }, cancellationToken);

    // One create inside an open write: the caller holds the project's lock. CreateAsync and CreateManyAsync share it.
    private async Task<WriteOutcome> CreateInAsync(
        WriteScope scope, WorkItemCreate create, CancellationToken cancellationToken, Slot backlogSlot = Slot.First, Guid? backlogBefore = null)
    {
        // The sequence is taken with an UPDATE, which holds the project row until commit: concurrent creates queue here, so keys never repeat.
        var reserved = await scope.Connection.QuerySingleOrDefaultAsync<ReservedKey>(new CommandDefinition(
            "UPDATE project SET next_item_number = next_item_number + 1 WHERE id = @id RETURNING key || '-' || (next_item_number - 1) AS key, next_item_number - 1 AS number",
            new { id = create.ProjectId }, scope.Transaction, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("Project not found.");
        FeatureFieldRules.CheckNew(create.Type, create.Slug, create.StartedOn);
        if (create.Slug is { } slug && await SlugTakenAsync(scope, create.ProjectId, slug, cancellationToken))
        {
            throw FeatureFieldRules.SlugTaken();
        }

        var stateId = await scope.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT id FROM state
            WHERE project_id = @projectId AND type = @type AND category = 'proposed' AND archived_at IS NULL
            ORDER BY sort_order LIMIT 1
            """,
            new { projectId = create.ProjectId, type = WorkItemTypes.Db(create.Type) }, scope.Transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.Validation($"A {create.Type} has no Proposed state.");
        var id = Guid.NewGuid();
        var seed = new ProposedChange(
            id, create.ProjectId, create.Type, create.Title, "", [], "", null, stateId, null, create.SprintId, 2, null, create.ParentId, [],
            null, false, null, null, await WorkItemOrdering.KeyAsync(scope, OrderScope.Backlog(create.ProjectId), id, backlogSlot, backlogBefore, cancellationToken),
            create.Goal, create.StatusLine, create.StatusLine.Length > 0 ? scope.At : null);
        var proposed = WorkItemProposals.Apply(seed, create.Changes, scope.At);
        var (columnId, laneId, columnOrder) = await WorkItemPlacement.InitialAsync(scope, create.ProjectId, id, create.Type, proposed.StateId, cancellationToken);
        proposed = proposed with { ColumnId = columnId, LaneId = laneId, ColumnOrder = columnOrder };
        var startedOn = create.Type == WorkItemTypeId.Feature ? create.StartedOn ?? DateOnly.FromDateTime(scope.At.UtcDateTime) : (DateOnly?)null;
        var item = new NewItem(reserved.Key, reserved.Number, create.Slug, startedOn);
        var result = await PersistAsync(scope, null, item, proposed, create.Changes, true, null, cancellationToken);
        return new WriteOutcome(result, null);
    }

    private static Task<bool> SlugTakenAsync(WriteScope scope, Guid projectId, string slug, CancellationToken cancellationToken)
        => scope.Connection.QuerySingleAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM work_item WHERE project_id = @projectId AND lower(slug) = lower(@slug))",
            new { projectId, slug }, scope.Transaction, cancellationToken: cancellationToken));

    private sealed record ReservedKey(string Key, int Number);
}
