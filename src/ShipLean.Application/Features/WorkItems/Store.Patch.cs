using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore
{
    public Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async scope =>
        {
            var row = (await LockAsync(scope, [id], cancellationToken))[id];
            if (row.Revision != patch.ExpectedRevision)
            {
                return (await StaleAsync(scope, row, patch.ExpectedRevision, cancellationToken), false);
            }

            await RunBeforePatchAsync(scope, options, cancellationToken);
            var result = await PatchOneAsync(scope, row, patch.Changes, cancellationToken);
            return (new WriteOutcome(result, null), true);
        }, cancellationToken);

    public Task<WriteManyOutcome> PatchManyAsync(IReadOnlyList<(Guid Id, WorkItemPatch Patch)> patches, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async scope =>
        {
            if (patches.Select(p => p.Id).Distinct().Count() != patches.Count)
            {
                throw ApiException.Validation("A work item can only appear once in a batch.");
            }

            var rows = await LockAsync(scope, [.. patches.Select(p => p.Id)], cancellationToken);
            var stale = new List<ConflictDto>();
            foreach (var (id, patch) in patches.Where(p => rows[p.Id].Revision != p.Patch.ExpectedRevision))
            {
                stale.Add(await _reads.ConflictAsync(scope.Connection, scope.Transaction, id, patch.ExpectedRevision, cancellationToken));
            }

            if (stale.Count > 0)
            {
                return (new WriteManyOutcome(null, stale), false);
            }

            await RunBeforePatchAsync(scope, options, cancellationToken);
            var written = new List<WriteResult>();
            foreach (var (id, patch) in patches)
            {
                written.Add(await PatchOneAsync(scope, rows[id], patch.Changes, cancellationToken));
            }

            return (new WriteManyOutcome(written, null), true);
        }, cancellationToken);

    private static async Task RunBeforePatchAsync(WriteScope scope, WriteOptions options, CancellationToken cancellationToken)
    {
        if (options.BeforePatch is { } hook)
        {
            await hook(scope.Transaction, cancellationToken);
        }
    }

    private async Task<WriteResult> PatchOneAsync(WriteScope scope, WorkItemRow row, WorkItemChanges changes, CancellationToken cancellationToken)
    {
        var links = await WorkItemQueries.LinksAsync(scope.Connection, scope.Transaction, [row.Id], cancellationToken);
        var before = row.ToSnapshot(links.GetValueOrDefault(row.Id) ?? []);
        var proposed = WorkItemProposals.Apply(WorkItemProposals.From(before), changes, scope.At);
        return await PersistAsync(scope, before, null, proposed, changes, true, null, cancellationToken);
    }
}
