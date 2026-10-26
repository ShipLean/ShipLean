using Dapper;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Runs the caller's hook, records the decided sprint inside the transaction, then throws so everything rolls back.
public sealed class HookObservingWorkItemStore(IWorkItemStore inner) : IWorkItemStore
{
    public static readonly List<string?> SeenInside = [];

    public Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
        => inner.PatchManyAsync(patches, options with
        {
            InTransaction = async (tx, ct) =>
            {
                if (options.InTransaction is { } hook)
                {
                    await hook(tx, ct);
                }

                SeenInside.Add(await tx.Connection!.ExecuteScalarAsync<string?>("SELECT string_agg(rollover_status, ',') FROM sprint WHERE rollover_status = 'decided' AND name LIKE 'Observed%'", transaction: tx));
                throw new InvalidOperationException("after the hook");
            },
        }, cancellationToken);

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
