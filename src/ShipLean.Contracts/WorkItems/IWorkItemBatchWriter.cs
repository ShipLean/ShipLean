namespace ShipLean.Contracts.WorkItems;

// Writes many rows in one transaction, leaving out the ones whose expected revision is stale instead of failing the batch.
// Takes the project's write lock and every row lock once, writes what isn't stale and places rows in request order:
// consecutive rows with an equal placement form a block that lands in request order. Two or more rows write with
// options.Bulk set by the caller. A separate contract from IWorkItemStore, as IWorkItemTreeWriter is, so that the store's
// test doubles don't change. The rows must all be in one project.
public interface IWorkItemBatchWriter
{
    // Throws as PatchAsync: ApiException.NotFound (unknown row), ApiException.Validation (bad field value, a block placed
    // before one of its own rows).
    Task<BatchWriteOutcome> WriteBatchAsync(IReadOnlyList<BatchWrite> items, WriteOptions options, CancellationToken cancellationToken = default);
}
