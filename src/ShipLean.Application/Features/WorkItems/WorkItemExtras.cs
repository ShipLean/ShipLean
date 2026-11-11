using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.WorkItems;

// An Epic's, Feature's or Story's roll-up counts (a Story's are its tasks; Epics and Features leave tasks out, as the backlog does) and
// what waits on the caller. Other types keep [] and null. A read with no signed-in user (ICurrentUser.Id throws then) gets no NeedsYou.
[RegisterPerRequest]
public sealed class WorkItemExtras(IWorkTreeReads reads, INeedsYouCounts counts, ICurrentUser currentUser) : IWorkItemExtras
{
    public async Task<WorkItemDto> ApplyAsync(WorkItemDto item, CancellationToken cancellationToken)
    {
        if (item.Type.Id is not (WorkItemTypeId.Epic or WorkItemTypeId.Feature or WorkItemTypeId.Story))
        {
            return item;
        }
        var isStory = item.Type.Id == WorkItemTypeId.Story;
        var rollups = (await reads.RollupsAsync(item.ProjectId, [item.Id], null, cancellationToken))
            .Where(r => isStory ? r.Type == WorkItemTypeId.Task : r.Type != WorkItemTypeId.Task)
            .Select(RollupCountDto.From)
            .ToList();
        NeedsYouDto? needsYou = null;
        if (CallerId() is { } userId)
        {
            needsYou = (await counts.ForItemsAsync(userId, [item.Id], cancellationToken)).GetValueOrDefault(item.Id);
        }
        return item with { Rollups = rollups, NeedsYou = needsYou };
    }

    private Guid? CallerId()
    {
        try
        {
            return currentUser.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
