namespace ShipLean.Contracts.WorkItems;

// Writes a tree of new work items (a Feature, its Stories and their Tasks) in one transaction. Each item is written as
// IWorkItemStore.CreateAsync writes one: same rules and checks, one WorkItemCreated event per item after commit. Keys are
// allocated parent-first in input order (an item, then its children, then its next sibling), and the whole tree lands at
// the top of the backlog in that same order, so siblings keep input order. All or nothing: any item that throws as
// CreateAsync does writes none of them. The InTransaction hook runs once per call. A separate contract from
// IWorkItemStore so that the store's test doubles don't have to implement it.
public interface IWorkItemTreeWriter
{
    // Throws as CreateAsync: ApiException.NotFound (unknown project or parent), ApiException.Validation (blank title,
    // parent type not allowed for the type, bad field value, a node whose Changes set a parent).
    Task<WorkItemTreeOutcome> CreateTreeAsync(WorkItemTreeCreate tree, WriteOptions options, CancellationToken cancellationToken = default);
}
