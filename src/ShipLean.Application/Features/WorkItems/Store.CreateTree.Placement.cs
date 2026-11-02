using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore : IWorkItemBlockWriter
{
    public async Task<IReadOnlyList<WriteResult>> CreateBlockAsync(WorkItemBlockCreate block, WriteOptions options, CancellationToken cancellationToken = default)
    {
        if (block.Items.Count == 0)
        {
            return [];
        }

        return await WriteAsync<IReadOnlyList<WriteResult>>(options, async scope =>
        {
            await WorkItemProjectLock.TakeAsync(scope, [block.ProjectId], cancellationToken);
            var placement = block.Placement ?? new BatchPlacement(BatchSlot.First, null);
            // At the top the block's first row goes first and the rest in front of what was on top before; at the end every row goes last;
            // before a row every row goes just in front of it, so they keep input order; a target that's gone or in another project means last.
            var top = placement.Slot == BatchSlot.First
                ? await scope.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                    "SELECT id FROM work_item WHERE project_id = @projectId ORDER BY backlog_order, id LIMIT 1",
                    new { projectId = block.ProjectId }, scope.Transaction, cancellationToken: cancellationToken))
                : null;
            var written = new List<WriteResult>();

            async Task AddAsync(WorkItemCreate create)
            {
                var (slot, before) = placement.Slot switch
                {
                    BatchSlot.First => (written.Count == 0 ? Slot.First : Slot.BeforeOrLast, top),
                    BatchSlot.Last => (Slot.Last, (Guid?)null),
                    _ => (Slot.BeforeOrLast, placement.BeforeItemId),
                };
                written.Add((await CreateInAsync(scope, create, cancellationToken, slot, before)).Written!);
            }

            foreach (var item in block.Items)
            {
                await AddAsync(item.Create);
                var parentId = written[^1].Id;
                foreach (var task in item.Tasks)
                {
                    await AddAsync(task with { ParentId = parentId });
                }
            }

            return (written, true);
        }, cancellationToken);
    }
}
