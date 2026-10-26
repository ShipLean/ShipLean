using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Mcp.Work;

// A store that passes everything to another; a test overrides only the calls it watches.
public abstract class DelegatingWorkItemStore(IWorkItemStore inner) : IWorkItemStore
{
    public virtual Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default)
        => inner.PatchAsync(id, patch, options, cancellationToken);

    public virtual Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default)
        => inner.CreateAsync(create, options, cancellationToken);

    public virtual Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default)
        => inner.CreateManyAsync(items, options, cancellationToken);

    public virtual Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
        => inner.PatchManyAsync(patches, options, cancellationToken);

    public virtual Task<WriteOutcome> MoveAsync(Guid id, MoveTarget target, WriteOptions options, CancellationToken cancellationToken = default)
        => inner.MoveAsync(id, target, options, cancellationToken);

    public virtual Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken = default)
        => inner.ReorderAsync(id, expectedRevision, beforeItemId, cancellationToken);

    public virtual Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken = default)
        => inner.RemoveAsync(id, expectedRevision, cancellationToken);

    public virtual Task<ImportResult> ImportAsync(Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken = default)
        => inner.ImportAsync(projectId, importJobId, item, cancellationToken);

    public virtual Task<WorkItemSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => inner.GetAsync(id, cancellationToken);

    public virtual Task<IReadOnlyList<RevisionSnapshot>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default)
        => inner.GetRevisionsAsync(id, cancellationToken);
}
