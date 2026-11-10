using Agoda.IoC.Core;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// What a single work item read adds after the read model is built, outside any transaction: the item's rollups and what waits
// on the caller. WorkItemReads calls it at the end of GetAsync and GetByKeyAsync only.
[RegisterPerRequest]
public sealed class WorkItemExtras
{
    // TODO: fill Rollups (IWorkTreeReads.RollupsAsync) and NeedsYou (INeedsYouCounts for the current user). With no current user
    // NeedsYou stays null and nothing throws; ICurrentUser.Id throws when there is no user, so decide that some other way. Until
    // then the item is returned unchanged.
    public Task<WorkItemDto> ApplyAsync(WorkItemDto item, CancellationToken cancellationToken) => Task.FromResult(item);
}
