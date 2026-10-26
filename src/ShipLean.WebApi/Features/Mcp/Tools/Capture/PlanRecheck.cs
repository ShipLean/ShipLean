using Agoda.IoC.Core;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

// A capture plan is sealed minutes before it is applied. Before it creates anything, check that the sprints and parents it
// names are still usable: the sprint is not archived, the parent is still in this project and not removed. The tool turns a
// reason into a conflict that says to preview again. Sprints are checked by the sealed id, so a rename does not stale a plan.
[RegisterPerRequest]
public sealed class PlanRecheck(SprintResolver sprints, IWorkItemStore store, IStateCatalog catalog)
{
    // Null when every item can still be created, otherwise why not.
    public async Task<string?> StaleReasonAsync(Guid projectId, IReadOnlyList<PlannedItem> planned, CancellationToken cancellationToken)
    {
        foreach (var sprint in planned.Where(p => p.SprintId is not null).DistinctBy(p => p.SprintId))
        {
            var live = await sprints.FindLiveAsync(projectId, sprint.SprintId!.Value, cancellationToken);
            if (live is null)
            {
                return $"Sprint {sprint.SprintName} is no longer available";
            }
        }

        var states = await catalog.ListAsync(projectId, cancellationToken);
        foreach (var parent in planned.Where(p => p.ParentId is not null).DistinctBy(p => p.ParentId))
        {
            var snapshot = await store.GetAsync(parent.ParentId!.Value, cancellationToken);
            var removed = states.Any(s => s.Id == snapshot.StateId && s.Category == StateCategory.Removed);
            if (snapshot.ProjectId != projectId || removed)
            {
                return $"{parent.ParentKey} was removed or moved to another project";
            }
        }
        return null;
    }
}
