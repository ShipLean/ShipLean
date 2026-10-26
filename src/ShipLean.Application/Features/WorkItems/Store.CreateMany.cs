using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    public async Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var projectId = items[0].ProjectId;
        if (items.Any(i => i.ProjectId != projectId))
        {
            throw ApiException.Validation("Work items created together must all be in one project.");
        }

        return await WriteAsync<IReadOnlyList<WriteOutcome>>(options, async scope =>
        {
            await WorkItemProjectLock.TakeAsync(scope, [projectId], cancellationToken);
            // The batch lands at the top in input order: the first goes first, the rest go in front of what was on top before the batch.
            var top = await scope.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM work_item WHERE project_id = @projectId ORDER BY backlog_order, id LIMIT 1",
                new { projectId }, scope.Transaction, cancellationToken: cancellationToken));
            var outcomes = new List<WriteOutcome>(items.Count);
            foreach (var create in items)
            {
                var slot = outcomes.Count == 0 ? Slot.First : Slot.BeforeOrLast;
                outcomes.Add(await CreateInAsync(scope, create, cancellationToken, slot, top));
            }

            return (outcomes, true);
        }, cancellationToken);
    }
}
