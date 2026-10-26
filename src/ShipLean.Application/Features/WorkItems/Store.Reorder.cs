using Dapper;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    // Position only: no rules, no revision.
    public Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken = default)
        => WriteAsync(WriteOptions.User, async scope =>
        {
            var row = (await LockAsync(scope, [id], cancellationToken))[id];
            if (row.Revision != expectedRevision)
            {
                return (await StaleAsync(scope, row, expectedRevision, cancellationToken), false);
            }

            var order = await WorkItemOrdering.KeyAsync(scope, OrderScope.Backlog(row.ProjectId), id, beforeItemId is null ? Slot.Last : Slot.Before, beforeItemId, cancellationToken);
            await scope.Connection.ExecuteAsync(new CommandDefinition(
                "UPDATE work_item SET backlog_order = @order WHERE id = @id", new { id, order }, scope.Transaction, cancellationToken: cancellationToken));
            scope.Publish([new WorkItemReordered(row.ProjectId, id, order)]);
            return (new WriteOutcome(new WriteResult(id, row.Key, row.Revision, [], []), null), true);
        }, cancellationToken);
}
