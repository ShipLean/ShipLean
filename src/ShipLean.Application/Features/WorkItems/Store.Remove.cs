using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    // The soft delete: the item enters its type's first Removed-category state; nothing is deleted. The rules do not run.
    public Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken = default)
        => WriteAsync(WriteOptions.User, async scope =>
        {
            var row = (await LockAsync(scope, [id], cancellationToken))[id];
            if (row.Revision != expectedRevision)
            {
                return (await StaleAsync(scope, row, expectedRevision, cancellationToken), false);
            }

            var removedState = await scope.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                """
                SELECT id FROM state
                WHERE project_id = @projectId AND type = @type AND category = 'removed' AND archived_at IS NULL
                ORDER BY sort_order LIMIT 1
                """,
                new { projectId = row.ProjectId, type = row.Type }, scope.Transaction, cancellationToken: cancellationToken))
                ?? throw ApiException.Validation($"A {row.Type} has no Removed state.");

            var links = await WorkItemQueries.LinksAsync(scope.Connection, scope.Transaction, [id], cancellationToken);
            var before = row.ToSnapshot(links.GetValueOrDefault(id) ?? []);
            var proposed = WorkItemProposals.From(before) with { StateId = removedState };
            var result = await PersistAsync(scope, before, null, proposed, null, false, null, cancellationToken);
            if (result.Changes.Any(c => c.Field == "state"))
            {
                scope.Publish([new WorkItemRemoved(row.ProjectId, id, row.ParentId)]);
            }

            return (new WriteOutcome(result, null), true);
        }, cancellationToken);
}
