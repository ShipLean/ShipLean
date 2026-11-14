using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Hosts;

// Patches already read from a code host, kept for a while so a Changes page does not call the host on every view. Memory, or
// Redis when more than one instance runs; CodePatchCacheServices (WebApi) picks one when the services are registered.
public interface ICodePatchCache
{
    Task<PatchRead?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, PatchRead value, TimeSpan lifetime, CancellationToken cancellationToken);
}
