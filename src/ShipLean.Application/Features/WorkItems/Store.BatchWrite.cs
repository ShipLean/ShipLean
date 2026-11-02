using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore : IWorkItemBatchWriter
{
    // TODO: take the project and row locks once, leave out stale rows, write the rest and place the blocks in one transaction; until then every row is answered as written and nothing is persisted.
    public async Task<BatchWriteOutcome> WriteBatchAsync(IReadOnlyList<BatchWrite> items, WriteOptions options, CancellationToken cancellationToken = default)
    {
        var written = new List<WriteResult>(items.Count);
        foreach (var item in items)
        {
            var row = await GetAsync(item.Id, cancellationToken);
            written.Add(new WriteResult(row.Id, row.Key, row.Revision, [], []));
        }

        return new BatchWriteOutcome(written, []);
    }
}
