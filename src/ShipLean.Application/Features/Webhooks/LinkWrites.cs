using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The write both the PR webhook and an agent's pull request link make: a patch retried with the item's current revision when
// someone saved it meanwhile.
internal static class LinkWrites
{
    private const int Attempts = 3;

    // False when every attempt was stale.
    public static async Task<bool> PatchAsync(IWorkItemStore store, Guid id, int revision, WorkItemChanges changes, WriteOptions options, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var outcome = await store.PatchAsync(id, new WorkItemPatch(revision, changes), options, cancellationToken);
            if (outcome.Written is not null)
            {
                return true;
            }
            revision = outcome.Conflict!.Current.Revision;
        }
        return false;
    }
}
