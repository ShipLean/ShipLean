namespace ShipLean.Contracts.WorkItems;

// Writes a block of new work items, each with its tasks, in one transaction and lands the block as one run in the backlog.
// Each item is written as IWorkItemStore.CreateAsync writes one (same rules, one WorkItemCreated event per item after
// commit). Keys are allocated in input order, an item and then its tasks. Placement null means the top. All or nothing.
// A separate contract from IWorkItemStore so that the store's test doubles don't have to implement it.
public interface IWorkItemBlockWriter
{
    // Throws as CreateAsync, and ApiException.NotFound when the Before target isn't in the project.
    // The results are in input order: an item, then its tasks.
    Task<IReadOnlyList<WriteResult>> CreateBlockAsync(WorkItemBlockCreate block, WriteOptions options, CancellationToken cancellationToken = default);
}
