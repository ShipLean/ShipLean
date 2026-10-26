using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Runs a change through another caller just before each batch patch, as a teammate editing between the rollover's read and its write.
public sealed class InterferingWorkItemStore(IWorkItemStore inner, Func<Task> interfere) : IWorkItemStore
{
    public async Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
    {
        await interfere();

        return await inner.PatchManyAsync(patches, options, cancellationToken);
    }

    public Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default) => inner.CreateAsync(create, options, cancellationToken);

    public Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default) => inner.CreateManyAsync(items, options, cancellationToken);

    public Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default) => inner.PatchAsync(id, patch, options, cancellationToken);

    public Task<WriteOutcome> MoveAsync(Guid id, MoveTarget target, WriteOptions options, CancellationToken cancellationToken = default) => inner.MoveAsync(id, target, options, cancellationToken);

    public Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken = default) => inner.ReorderAsync(id, expectedRevision, beforeItemId, cancellationToken);

    public Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken = default) => inner.RemoveAsync(id, expectedRevision, cancellationToken);

    public Task<ImportResult> ImportAsync(Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken = default) => inner.ImportAsync(projectId, importJobId, item, cancellationToken);

    public Task<WorkItemSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAsync(id, cancellationToken);

    public Task<IReadOnlyList<RevisionSnapshot>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetRevisionsAsync(id, cancellationToken);
}
