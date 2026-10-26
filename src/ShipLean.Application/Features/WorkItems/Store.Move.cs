using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    // A drop on a board or the taskboard: position, the Set patch and the rules in one write, one revision when a field changed.
    public Task<WriteOutcome> MoveAsync(Guid id, MoveTarget target, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async scope =>
        {
            var row = (await LockAsync(scope, [id], cancellationToken))[id];
            if (row.Revision != target.ExpectedRevision)
            {
                return (await StaleAsync(scope, row, target.ExpectedRevision, cancellationToken), false);
            }

            await CheckTargetAsync(scope, row, target, cancellationToken);

            var links = await WorkItemQueries.LinksAsync(scope.Connection, scope.Transaction, [id], cancellationToken);
            var before = row.ToSnapshot(links.GetValueOrDefault(id) ?? []);
            var proposed = WorkItemProposals.Apply(WorkItemProposals.From(before), target.Set, scope.At) with
            {
                ColumnId = target.ColumnId,
                ColumnDone = target.Done,
                LaneId = target.LaneId ?? before.LaneId,
            };
            var slot = target.BeforeItemId is null ? Slot.Last : Slot.Before;
            proposed = proposed with
            {
                ColumnOrder = OrderScope.Of(proposed) is { } orderScope
                    ? await WorkItemOrdering.KeyAsync(scope, orderScope, id, slot, target.BeforeItemId, cancellationToken)
                    : null,
            };

            var result = await PersistAsync(scope, before, null, proposed, target.Set, true, target.BeforeItemId, cancellationToken);
            var after = (await WorkItemQueries.RowsAsync(scope.Connection, scope.Transaction, $"{WorkItemRow.Select} WHERE w.id = @id", new { id }, cancellationToken))[0];
            var positionChanged = after.ColumnId != row.ColumnId || after.ColumnDone != row.ColumnDone || after.LaneId != row.LaneId || after.ColumnOrder != row.ColumnOrder;
            if (positionChanged || (result.Changes.Count > 0 && after.ColumnOrder is not null))
            {
                scope.Publish([new WorkItemMoved(after.ProjectId, id, after.Revision, after.ColumnId, after.ColumnDone, after.LaneId, after.ColumnOrder, after.StateId, after.ParentId)]);
            }

            return (new WriteOutcome(result, null), true);
        }, cancellationToken);

    // A card on a board level needs a column; only a task has a column-less drop (the Taskboard). The level's board is read
    // FOR SHARE, so a settings put cannot remove the column while a card lands in it.
    private static async Task CheckTargetAsync(WriteScope scope, WorkItemRow row, MoveTarget target, CancellationToken cancellationToken)
    {
        var level = WorkItemTypes.BoardLevel(WorkItemTypes.Parse(row.Type));
        if (target.ColumnId is null)
        {
            if (level is not null)
            {
                throw ApiException.Validation($"A {row.Type} can only be dropped in a board column.");
            }

            return;
        }

        if (level is null)
        {
            throw ApiException.Validation($"A {row.Type} isn't on a board with columns.");
        }

        var json = await scope.Connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT settings::text FROM board WHERE project_id = @projectId AND level = @level FOR SHARE",
            new { projectId = row.ProjectId, level }, scope.Transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.Validation("The project has no board to move the card on.");
        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(json, PersistedJson.Options)!;
        var column = settings.Columns.FirstOrDefault(c => c.Id == target.ColumnId) ?? throw ApiException.Validation("That column isn't on the board.");
        if (target.Done && !column.Split)
        {
            throw ApiException.Validation("That column has no Done half.");
        }

        if (target.LaneId is { } lane && settings.Swimlanes.All(l => l.Id != lane))
        {
            throw ApiException.Validation("That lane isn't on the board.");
        }
    }
}
