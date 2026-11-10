using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Reads;

// What the asks read endpoint does: the access check first, then the read. Any member reads a work item's asks.
[RegisterPerRequest]
public sealed class AskEndpoints(IProjectAccess access, IItemAsks asks)
{
    public async Task<ItemAsksDto> GetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await asks.GetAsync(workItemId, cancellationToken);
    }
}
