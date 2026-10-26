using Dapper;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore : IWorkItemTreeWriter
{
    public async Task<WorkItemTreeOutcome> CreateTreeAsync(WorkItemTreeCreate tree, WriteOptions options, CancellationToken cancellationToken = default)
    {
        if (tree.Items.Count == 0)
        {
            return new WorkItemTreeOutcome([]);
        }

        return await WriteAsync(options, async scope =>
        {
            await WorkItemProjectLock.TakeAsync(scope, [tree.ProjectId], cancellationToken);
            // As CreateMany: the tree lands at the top in key order, the first item first and the rest in front of what was on top before.
            var top = await scope.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM work_item WHERE project_id = @projectId ORDER BY backlog_order, id LIMIT 1",
                new { projectId = tree.ProjectId }, scope.Transaction, cancellationToken: cancellationToken));
            var first = true;

            async Task<IReadOnlyList<WorkItemTreeOutcome.Node>> CreateAllAsync(IReadOnlyList<WorkItemTreeCreate.Node> nodes, Guid? parentId)
            {
                var created = new List<WorkItemTreeOutcome.Node>(nodes.Count);
                foreach (var node in nodes)
                {
                    var written = (await CreateInAsync(scope, node.CreateUnder(tree.ProjectId, parentId), cancellationToken, first ? Slot.First : Slot.BeforeOrLast, top)).Written!;
                    first = false;
                    created.Add(new(written.Id, written.Key, await CreateAllAsync(node.Children, written.Id)));
                }

                return created;
            }

            return (new WorkItemTreeOutcome(await CreateAllAsync(tree.Items, tree.ParentId)), true);
        }, cancellationToken);
    }
}
