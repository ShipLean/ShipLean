using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// An IWorkItemStore whose batch patch fails, for the all-or-nothing rollover test.
public sealed class ThrowingWorkItemStore : IWorkItemStore
{
    public Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The work item store failed.");

    public Task<WriteOutcome> CreateAsync(WorkItemCreate create, WriteOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<WriteOutcome>> CreateManyAsync(IReadOnlyList<WorkItemCreate> items, WriteOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<WriteOutcome> MoveAsync(Guid id, MoveTarget target, WriteOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ImportResult> ImportAsync(Guid projectId, Guid importJobId, ImportedWorkItem item, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<WorkItemSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<RevisionSnapshot>> GetRevisionsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
