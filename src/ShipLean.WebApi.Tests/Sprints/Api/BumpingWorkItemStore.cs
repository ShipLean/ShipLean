using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Changes the first item through the real store just before the batch patch, so every revision the rollover read is stale for that item.
public sealed class BumpingWorkItemStore(IWorkItemStore inner) : IWorkItemStore
{
    public async Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
    {
        var first = patches[0].Id;
        var now = await inner.GetAsync(first, cancellationToken);
        await inner.PatchAsync(first, new WorkItemPatch(now.Revision, new WorkItemChanges { Title = new FieldSet<string>("Bumped by someone else") }), WriteOptions.User, cancellationToken);
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
