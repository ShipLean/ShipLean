using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// What a single work item read adds after the read model is built, outside any transaction: the item's rollups and what waits
// on the caller. WorkItemReads calls it at the end of GetAsync and GetByKeyAsync only.
public interface IWorkItemExtras
{
    Task<WorkItemDto> ApplyAsync(WorkItemDto item, CancellationToken cancellationToken);
}
