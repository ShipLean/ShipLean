using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.Mcp.Work;

// The real store, except that PatchAsync first lets StoreInterference make another person's change. Everything else passes through.
public sealed class InterferingWorkItemStore(IWorkItemStore inner, StoreInterference interference) : DelegatingWorkItemStore(inner)
{
    private readonly IWorkItemStore _inner = inner;

    public override async Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, WriteOptions options, CancellationToken cancellationToken = default)
    {
        interference.CountPatch();
        if (interference.Take() is { } theirs)
        {
            var current = await _inner.GetAsync(id, cancellationToken);
            await _inner.PatchAsync(id, new WorkItemPatch(current.Revision, theirs), options, cancellationToken);
        }

        return await _inner.PatchAsync(id, patch, options, cancellationToken);
    }
}
