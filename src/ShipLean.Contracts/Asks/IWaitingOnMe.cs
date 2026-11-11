namespace ShipLean.Contracts.Asks;

// Open asks on the items the signed-in person owns: coalesce(assigned_to, created_by) is me, a live project_member row for
// me (archived_at is null), and the item's state category is not removed. Neither work items nor projects have an archived column.
public interface IWaitingOnMe
{
    Task<WaitingDto> GetAsync(CancellationToken cancellationToken = default);
}
