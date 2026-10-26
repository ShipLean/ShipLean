namespace ShipLean.Application.Features.Mcp.Planning.Status;

// What a plan's status table reads beyond the tree: when each task was first picked up and first finished, its commit links and
// its review comments. Callers check project access first.
public interface IPlanStatusReads
{
    // One entry per task id given; a task with no history, links or comments gets an empty activity.
    Task<IReadOnlyDictionary<Guid, PlanTaskActivity>> ActivityAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken = default);
}
