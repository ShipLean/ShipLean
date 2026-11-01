namespace ShipLean.Contracts.Retros;

// The retro cards that concern one work item.
public interface IWorkItemRetroReader
{
    Task<WorkItemRetrosDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
